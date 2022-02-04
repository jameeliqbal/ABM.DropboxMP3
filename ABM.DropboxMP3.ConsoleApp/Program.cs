

using Dropbox.Api;
using Dropbox.Api.Files;
using IKriv.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ABM.DropboxMP3.ConsoleApp
{
    class Program
    {
        // Add an ApiKey (from https://www.dropbox.com/developers/apps) here
        public string ApiKey { get; set; } 
        public string AppToken { get; set; }
        public string AppSecret { get; set; }
        public int WaitingTimeForConverting { get; set; }
        public string ConvertedFileExtension { get; set; }
       
        [STAThread]
        static int Main(string[] args)
        {

            var instance = new Program();

            try
            {
                Console.WriteLine("*** Upload file to Dropbox Demo ***");

                //run in a seperate thread to avoid freezing the ui
                var task = Task.Run((Func<Task<int>>)instance.Run);

                task.Wait();

                return task.Result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
            }

         }

        private async Task<int> Run()
        {
            DropboxCertHelper.InitializeCertPinning();
            InitializeSettings();

            // Specify socket level timeout which decides maximum waiting time when no bytes are
            // received by the socket.
            var httpClient = new HttpClient(new WebRequestHandler { ReadWriteTimeout = 10 * 1000 })
            {
                // Specify request level timeout which decides maximum time that can be spent on
                // download/upload files.
                Timeout = TimeSpan.FromMinutes(20)
            };

            try
            {
                //create the configuration object. UserAgent parameter can be any name.
                var config = new DropboxClientConfig("AyeshwaryaAudioConvertor")
                {
                    HttpClient = httpClient
                };

                //instantiate the client
                var client = new DropboxClient(AppToken, null, ApiKey, AppSecret, config);

                //// This call should succeed since the correct scope has been acquired
                await GetCurrentAccount(client);


                Console.WriteLine("******************************************************");
                Console.WriteLine("!!! Account Authenticated and connected to DropBox !!!");
                Console.WriteLine("******************************************************");

                //accept filename and upload the file
                var filename = await UploadMP3(client);

                //display list of files in the dropbox folder once the file is uploaded.
                await ListFilesInDropboxFolder(client);

                //timer to wait for file to get converted
                var convertedFile = await WaitForFileToConvert(client, filename, WaitingTimeForConverting, ConvertedFileExtension, new CancellationTokenSource());

                //once the converted file is found, download it.
                Console.WriteLine("Press any key to download file");
                Console.ReadKey();
                await DownloadConvertedFile(client, convertedFile);

                //delete the original and converted files from the dropbox folder.
                var filesToDelete = new List<string>{ filename, convertedFile };
                await DeleteFilesOnDropbox(client, filesToDelete);

                Console.WriteLine("Exit with any key");
                Console.ReadKey();
            }
            catch (HttpException e)
            {
                Console.WriteLine("Exception reported from RPC layer");
                Console.WriteLine("    Status code: {0}", e.StatusCode);
                Console.WriteLine("    Message    : {0}", e.Message);
                if (e.RequestUri != null)
                {
                    Console.WriteLine("    Request uri: {0}", e.RequestUri);
                }
                Console.WriteLine("Exit with any key");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("***ERRor***");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);
                Console.WriteLine("Exit with any key");
                Console.ReadKey();
            }

            return 0;
        }

        private void InitializeSettings()
        {//read the values from config file
            ApiKey = ConfigurationManager.AppSettings["ApiKey"];
            AppToken= ConfigurationManager.AppSettings["AppToken"];
            AppSecret= ConfigurationManager.AppSettings["AppSecret"];
            WaitingTimeForConverting = int.Parse(ConfigurationManager.AppSettings["WaitingTimeForConverting"]);
            ConvertedFileExtension = ConfigurationManager.AppSettings["ConvertedFileExtension"];
        }


        /// <summary>
        /// Gets information about the currently authorized account.
        /// <para>
        /// This demonstrates calling a simple rpc style api from the Users namespace.
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <returns>An asynchronous task.</returns>
        private async Task GetCurrentAccount(DropboxClient client)
        {
            try
            {
                Console.WriteLine("Current Account:");
                var full = await client.Users.GetCurrentAccountAsync();

                Console.WriteLine("Account id    : {0}", full.AccountId);
                Console.WriteLine("Country       : {0}", full.Country);
                Console.WriteLine("Email         : {0}", full.Email);
                Console.WriteLine("Is paired     : {0}", full.IsPaired ? "Yes" : "No");
                Console.WriteLine("Locale        : {0}", full.Locale);
                Console.WriteLine("Name");
                Console.WriteLine("  Display  : {0}", full.Name.DisplayName);
                Console.WriteLine("  Familiar : {0}", full.Name.FamiliarName);
                Console.WriteLine("  Given    : {0}", full.Name.GivenName);
                Console.WriteLine("  Surname  : {0}", full.Name.Surname);
                Console.WriteLine("Referral link : {0}", full.ReferralLink);

                if (full.Team != null)
                {
                    Console.WriteLine("Team");
                    Console.WriteLine("  Id   : {0}", full.Team.Id);
                    Console.WriteLine("  Name : {0}", full.Team.Name);
                }
                else
                {
                    Console.WriteLine("Team - None");
                }
            }
            catch (Exception e)
            {
                throw e;
            }

        }


        /// <summary>
        /// Upload files to dropbox
        /// <para>
        /// Gets filename as input from user and uploads the file to dropbox
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <returns>A string containing the name of the uploaded file</returns>
        private async Task<string> UploadMP3(DropboxClient client)
        {

            string filename = string.Empty;
            try
            {
                //accept the filename as input
                Console.WriteLine("Enter a filename:");
                filename = Console.ReadLine();
                Console.WriteLine(filename);

                // upload it to dropbox
                using (var mem = new MemoryStream(File.ReadAllBytes(filename)))
                {
                    var updated = await client.Files.UploadAsync(
                          "/" + filename,
                        WriteMode.Overwrite.Instance,
                        body: mem);
                    Console.WriteLine("Saved {0}/{1} rev {2}", null, filename, updated.Rev);
                }
            }
            catch
            {
                throw;

            }
            return filename;
        }


        /// <summary>
        /// List files on dropbox
        /// <para>
        /// Gets a list of files and folders on dropbox
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <returns>An asynchronous task.</returns>
        async Task ListFilesInDropboxFolder(DropboxClient dbx)
        {
            var list = await dbx.Files.ListFolderAsync(string.Empty);
            Console.WriteLine("Listing files...");

            // show folders then files
            foreach (var item in list.Entries.Where(i => i.IsFolder))
            {
                Console.WriteLine("D  {0}/", item.Name);
            }

            foreach (var item in list.Entries.Where(i => i.IsFile))
            {
                Console.WriteLine("F{0,8} {1}", item.AsFile.Size, item.Name);
            }
        }

        /// <summary>
        /// Waits for a specified period of time and calls VerifyIfFileIsConverted() method
        /// <para>
        /// This method waits for a specified period of time and checks the dropbox folder
        /// to see if the converted file is created.  If not it waits further.
        /// The timer is set using TaskTimer which is passed the waiting period and cancellation token.
        /// After the task is executed at the specified time the cancellation token is used to stop the task.
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <param name="filename">The name of the file that is uploaded</param>
        /// <param name="waitingTime">The time to wait before checking for the converted file</param>
        /// <param name="convertedFileExtension">The extension of the converted file to look for</param>
        /// <param name="cts">The cancellation token source used to cancel the task on completion</param> 
        /// <returns>A string containing the file name of the converted file.</returns>
        private async Task<string> WaitForFileToConvert(DropboxClient client, string filename, int waitingTime, string convertedFileExtension, CancellationTokenSource cts)
        {
            string convertedFilename = string.Empty;
            //get the cancellation token from the cancellation token source
            var ctoken = cts.Token;

            //start the timer
            using (var timer = new TaskTimer(waitingTime).CancelWith(ctoken).Start())
            {
                try
                {
                    //invoke the cancellation error if task is cancelled.
                    ctoken.ThrowIfCancellationRequested();

                    //wait for the task
                    foreach (var task in timer)
                    {
                        await task;

                        //call the method when the time is reached
                        convertedFilename = await VerifyIfFileIsConverted(client, filename, convertedFileExtension);
                        if (!string.IsNullOrEmpty(convertedFilename))
                        {
                            //stop the timer once the verification is complete
                            cts.Cancel();
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    return convertedFilename;
                }
                catch
                {
                    throw;
                }
            }

            return string.Empty;
        }


        /// <summary>
        /// Look for converted file in dropbox folder
        /// <para>
        /// Looks up for the converted file in the dropbox folder 
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <param name="filename">The name of the file that is uploaded</param>
        /// <param name="convertedFileExtension">The extension of the converted file to look for</param>
        /// <returns>A string containing the file name of the converted file.</returns>
        private async Task<string> VerifyIfFileIsConverted(DropboxClient client, string filename, string convertedFileExtension)
        {
            string convertedFileName = Path.GetFileNameWithoutExtension(filename) + convertedFileExtension;
            try
            {
                //look for the converted file on dropbox folder
                var list = await client.Files.ListFolderAsync(string.Empty);
                var file = list.Entries.SingleOrDefault(f => f.Name == convertedFileName);
                if (file != null)
                {
                    Console.WriteLine("!!! File converted successfully !!!");
                    return convertedFileName;
                }
                else
                {
                    Console.WriteLine("Waiting for file...");
                    return string.Empty;
                }


            }
            catch
            {

                throw;
            }
        }


        /// <summary>
        /// Downlaod converted file from dropbox
        /// <para>
        /// Downloads the converted file from dropbox  
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <param name="convertedFile">Name of the file to download</param>
        /// <returns>An asynchronous task.</returns>
        private async Task DownloadConvertedFile(DropboxClient client, string convertedFile)
        {
            try
            {
                Console.WriteLine("Downloading file: " + convertedFile);
                using (var response = await client.Files.DownloadAsync( "/" + convertedFile))
                {
                    var content= await response.GetContentAsByteArrayAsync();
                    File.WriteAllBytes(convertedFile, content);
                }
                Console.WriteLine("!!! File downloaded successfully !!!");
            }
            catch 
            {

                throw;
            }
        }


        /// <summary>
        /// Delete files on dropbox
        /// <para>
        /// Deletes the original and converted files on dropbox in bulk
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <param name="filesToDelete">List of files to delete</param>
        /// <returns>An asynchronous task.</returns>
        private async Task DeleteFilesOnDropbox(DropboxClient client, IEnumerable<string> filesToDelete)
        {
            try
            {
                Console.WriteLine("Deleting Files on Dropbox...");
                var ftdList = new List<DeleteArg>();
                foreach (var item in filesToDelete)
                {
                    var ftd = new DeleteArg("/" + item);
                    ftdList.Add(ftd);
                }

                await client.Files.DeleteBatchAsync(ftdList);
                Console.WriteLine("!!! Files Deleted Successfully !!!");
            }
            catch
            {

                throw;
            }
        }


    }
}
