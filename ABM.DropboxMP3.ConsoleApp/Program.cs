using Dropbox.Api;
using Dropbox.Api.Files;
using IKriv.Threading.Tasks;
using System;
using System.Collections.Generic;
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
        public string ApiKey { get; set; } = "kiq8ynfjfaiz26m";
        public string AppToken { get; set; } = "sl.BBUDB0zT_uZtzRuO9nSVe7igEUmwO8xDtheGueRjxIXrAFHUapqYxiuYhEzB7nKBrrZHXxS5lpB6onRNWxw6Hag0NjrETMVSSvDdlks2AHGcMwxDIoJb_dbrl1_jjrLESYpoej8";
        public string AppSecret { get; set; } = "gqd54jst58hs0g4";

        [STAThread]
        static int Main(string[] args)
        {

            var instance = new Program();

            try
            {
                Console.WriteLine("*** Upload file to Dropbox Demo ***");
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
                var config = new DropboxClientConfig("AyeshwaryaAudioConvertor")
                {
                    HttpClient = httpClient
                };
                var client = new DropboxClient(AppToken, null, ApiKey, AppSecret, config);
                //// This call should succeed since the correct scope has been acquired
                await GetCurrentAccount(client);


                Console.WriteLine("******************************************************");
                Console.WriteLine("!!! Account Authenticated and connected to DropBox !!!");
                Console.WriteLine("******************************************************");

                var filename = await UploadMP3(client);

                await ListRootFolder(client);

                var convertedFile = await WaitForFileToConvert(client, filename, new CancellationTokenSource());

                Console.WriteLine("Press any key to download file");
                Console.ReadKey();
                await DownloadConvertedFile(client, convertedFile);

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

        private async Task<string> WaitForFileToConvert(DropboxClient client, string filename,CancellationTokenSource cts)
        {
            string convertedFilename = string.Empty;
            var ctoken = cts.Token;
            using (var timer = new TaskTimer(10000).CancelWith(ctoken).Start())
            {
                try
                {
                    ctoken.ThrowIfCancellationRequested();
                    foreach (var task in timer)
                    {
                        await task;
                        convertedFilename = await VerifyIfFileIsConverted(client, filename);
                        if (!string.IsNullOrEmpty(convertedFilename)) 
                        {
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
                Console.WriteLine("!!! File downloaded successfully");
            }
            catch 
            {

                throw;
            }
        }

        private async Task<string> VerifyIfFileIsConverted(DropboxClient client, string filename)
        {
            string convertedFileName = Path.GetFileNameWithoutExtension(filename) + ".conv";
            try
            {

                var list = await client.Files.ListFolderAsync(string.Empty);
                var file= list.Entries.SingleOrDefault(f => f.Name == convertedFileName);
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

        private async Task<string> UploadMP3(DropboxClient client)
        {
            string filename = string.Empty;
            try
            {
                Console.WriteLine("Enter a filename:");
                  filename = Console.ReadLine();
                Console.WriteLine(filename);


                await Upload(client, null, filename, null);

            }
            catch
            {
                throw;
                
            }
            return filename;
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

        async Task ListRootFolder(DropboxClient dbx)
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

        async Task Upload(DropboxClient client, string folder, string file, string content)
        {
            using (var mem = new MemoryStream(File.ReadAllBytes(file)))
            {
                var updated = await client.Files.UploadAsync(
                      "/" + file,
                    WriteMode.Overwrite.Instance,
                    body: mem);
                Console.WriteLine("Saved {0}/{1} rev {2}", folder, file, updated.Rev);
            }
        }
    }
}
