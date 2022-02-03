using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ABM.DropboxMP3.ConsoleApp
{
    class Program
    {
        // Add an ApiKey (from https://www.dropbox.com/developers/apps) here
        public string ApiKey { get; set; } = "kiq8ynfjfaiz26m";
        public string AppToken { get; set; } = "sl.BBRAsG3mNk5peA5UIJO0XXcqjEzayQj89uxIanMPJw6aSy_GulS7dkc-q7hxmu4_9e9xuDyN7p-v8gljAVZa_EEforGr0RYE3mbksj0O3Nsv7-m_e8jdsOd9XlLLLNkUeScPRJQ";
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

            Console.WriteLine("Enter a filename:");
            var filename=Console.ReadLine();
            Console.WriteLine(filename);
            Console.ReadKey();
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
                //await GetCurrentAccount(client);
                //Console.WriteLine("Oauth PKCE Test Complete!");

                //await  ListRootFolder(client);
                var file = "ft-desktop-5.txt";
                await Upload(client, null, file, null);

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
            }
            catch (Exception ex)
            {
                Console.WriteLine("***ERRor***");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);
            }

            return 0;
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

        async Task Upload(DropboxClient dbx, string folder, string file, string content)
        {
            using (var mem = new MemoryStream(File.ReadAllBytes(file)))
            {
                var updated = await dbx.Files.UploadAsync(
                      "/" + file,
                    WriteMode.Overwrite.Instance,
                    body: mem);
                Console.WriteLine("Saved {0}/{1} rev {2}", folder, file, updated.Rev);
            }
        }
    }
}
