using System.Net;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using Newtonsoft.Json;

namespace Stallion
{
    public partial class Form1 : Form
    {
        // TODO: Move to localized resources
        private string baseGameName = "Burnout Paradise Remastered";

        // TODO: Move to configuration file
        private string? installLocation;
        private string apiURL = "https://api.github.com";
        private string username = "FrowningToad";
        private string repo = "burnout_mods";

        // TODO: Move all external logic out of form
        private List<GitModsListing>? _gitModListings;
        private HttpClient? _httpClient;
        private ListView? _selectedListView;

        // BIG TODO: Move downloading functionality out of user interface

        public Form1()
        {
            InitializeComponent();
            ConstructHttpAPI(apiURL);
            CreateTabsFromRepo();

            SetCurrentListView();
        }

        void CreateTabsFromRepo()
        {
            tabControl1.TabPages.Clear();

            _gitModListings = new List<GitModsListing>();

            // Get API request for root folders
            var modTypeAsync = GetListingsFromAPIAsync(username, repo).Result;

            // Mod type sanity check
            if (modTypeAsync != null)
            {
                modTypeAsync = FilterGitObjects(modTypeAsync, GitObjectFilterType.Directory);
            }
            else
            {
                goto ERROR;
            }

            if (modTypeAsync != null)
            {
                foreach (var modTypeFolder in modTypeAsync.Select((value, index) => new { value, index }))
                {
                    GitModsListing modFolderListing = new();
                    
                    tabControl1.TabPages.Add(modTypeFolder.value.name);

                    // Construct ListView
                    ListView lV = new()
                    {
                        Location = Point.Empty,
                        Parent = tabControl1.TabPages[modTypeFolder.index],
                        Dock = DockStyle.Fill,
                        View = View.List
                    };

                    // Get API request for subfolders (mods)
                    var modFolders = GetListingsFromAPIAsync(username, repo, modTypeFolder.value.name).Result;
                    modFolderListing.Listings = modFolders;

                    // Add ListView items
                    for (var i = 0; i < modFolders.Count; i++)
                    {
                        var gitObjects = GetListingsFromAPIAsync(username, repo, $"{modTypeFolder.value.name}/{modFolders[i].name}").Result;
                        
                        if (gitObjects == null) continue;
                        
                        var modJsons =
                            FilterGitObjects(gitObjects, GitObjectFilterType.Json);

                        foreach (var modListing in modJsons.Select((value, index) => new { value, index }))
                        {
                            var rawJson = _httpClient.GetAsync(modListing.value.download_url).Result.Content.ReadAsStringAsync().Result + "\n";

                            var gitObject = modFolderListing.Listings[i];

                            // Hack cuz json deserializer doesn't like non-listed types
                            var deserializedJson = JsonConvert.DeserializeObject<List<ModJson>>(rawJson);
                            gitObject.Json = deserializedJson?[0];

                            modFolderListing.Listings[i] = gitObject;

                            lV.Items.Add(((ModJson)gitObject.Json!).mod_name);
                        }
                    }

                    _gitModListings.Add(modFolderListing);
                }
            }
            else
            {
                goto ERROR;
            }
            return;

            ERROR:
            MessageBox.Show(
                $"CreateTabsFromRepo: Failed to retrieve {baseGameName} mods from {username}/{repo}. Reason: No mod types were found in the repository.",
                Application.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation
            );
        }

        void ConstructHttpAPI(string baseURL)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseURL)
            };

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/109.0");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        async Task<List<GitObject>?> GetListingsFromAPIAsync(string username, string repository, string subfolders = "")
        {
            var finalURL = $"/repos/{username}/{repository}/contents/{subfolders}";
            var listings = new List<GitObject>();

            if (_httpClient != null)
            {
                var response = _httpClient.GetAsync(finalURL).Result;

                if (response.IsSuccessStatusCode)
                {
                    // request was successful, continue processing the response
                    var responseContent = await response.Content.ReadAsStringAsync() + "\n";
                    listings = JsonConvert.DeserializeObject<List<GitObject>>(responseContent);

                    return listings;
                }

                MessageBox.Show(
                    $"GetListingsFromAPIAsync: Failed to retrieve {baseGameName} mods from {apiURL}{finalURL}. Reason: {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                    $"-- Debug --\n" +
                    $"Headers: {response.RequestMessage.Headers}\nMethod: {response.RequestMessage.Method}\nRequest URI: {response.RequestMessage.RequestUri}",
                    Application.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return listings;
            }
            
            // Reconstruct HTTP client and try again

            if (_httpClient == null)
            {
                ConstructHttpAPI(apiURL);
            }

            return await GetListingsFromAPIAsync(username, repository, subfolders);
        }

        List<GitObject> FilterGitObjects(List<GitObject> list, GitObjectFilterType filterType)
        {
            string filter;

            switch (filterType)
            {
                case GitObjectFilterType.Directory:
                    filter = "dir";
                    break;
                case GitObjectFilterType.Json:
                case GitObjectFilterType.File:
                    filter = "file";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filterType), filterType, null);
            }

            var outList = new List<GitObject>();

            foreach (var listing in list.Select((value, index) => new { value, index }))
            {
                if (listing.value.type != filter)
                {
                    outList.Add(listing.value);
                }
                else switch (filterType)
                {
                    case GitObjectFilterType.Json when listing.value.name.Contains(".json"):
                        outList.Add(listing.value);
                        break;
                    case GitObjectFilterType.Directory:
                        break;
                    case GitObjectFilterType.File:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(filterType), filterType, null);
                }
            }

            foreach (var item in outList)
            {
                list.Remove(item);
            }

            return list;
        }

        bool InstallSelectedMods()
        {
            if (installLocation != string.Empty)
            {
                ShowInstallLocationFolderSelectDialog();
            }
            else
            {

            }

            MessageBox.Show(
                $"Still working on this functionality, please try again with a newer build!",
                Application.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Asterisk
            );

            return true;

            // For Later :)

            for (var i = 0; i < _selectedListView.SelectedIndices.Count; i++)
            {
                if (InstallMod(_gitModListings[tabControl1.SelectedIndex].Listings[i])) continue;

                MessageBox.Show(
                    $"Failed to install {_gitModListings[tabControl1.SelectedIndex].Listings[i].name}!",
                    Application.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation
                );
                return false;
            }

            return true;
        }

        bool InstallMod(GitObject mod)
        {
            // TODO: Will finish once I stop being rate limited by GitHub :)
            return true;
        }

        struct GitModsListing
        {
            public List<GitObject>? Listings { get; set; }
        }

        struct GitObject : IJsonData
        {
            public string name { get; set; }
            public string path { get; set; }
            public string sha { get; set; }
            public int size { get; set; }
            public string type { get; set; }
            public string download_url { get; set; }
            public IJsonData? Json { get; set; }
        }

        struct ModJson : IJsonData
        {
            public string mod_name { get; set; }
            public string mod_author { get; set; }
            public string mod_version { get; set; }
            public string mod_description { get; set; }
            public string mod_image_link { get; set; }
        }

        interface IJsonData  { /* Implement abstract json data if needed */ }

        enum GitObjectFilterType
        {
            Directory,
            File,
            Json
        }

        #region UI Handlers
        private void installToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (installLocation != String.Empty)
            {
                InstallSelectedMods();
            }
            else
            {
                ShowInstallLocationFolderSelectDialog();
            }
        }

        private void infoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var rotatedBmp = new Bitmap(384, 384);

            using (var g = Graphics.FromImage(rotatedBmp))
            {
                g.TranslateTransform(Properties.Resources.TestImage.Width / 1.578f, Properties.Resources.TestImage.Height / 1.9f);
                g.RotateTransform(-10.95f);
                g.TranslateTransform(-Properties.Resources.TestImage.Width / 2, -Properties.Resources.TestImage.Height / 2);
                g.ScaleTransform(0.75f, 0.75f);

                g.DrawImage(Properties.Resources.TestImage.GetThumbnailImage(384, 384, null, IntPtr.Zero), new Point(0, 0));

                g.Dispose();
            }

            pictureBox1.BackgroundImage = rotatedBmp;

            //rotatedBmp.Dispose();
        }

        private void chooseInstallLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowInstallLocationFolderSelectDialog();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetCurrentListView();
        }

        private void ShowInstallLocationFolderSelectDialog()
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                installLocation = folderBrowserDialog1.SelectedPath;
            }
            else
            {
                MessageBox.Show(
                    $"Please select a valid {baseGameName} installation location.",
                    Application.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation
                );
            }
        }

        private void SetCurrentListView()
        {
            if (tabControl1.SelectedTab.HasChildren)
            {
                // HACKTACULAR
                _selectedListView = (ListView)tabControl1.SelectedTab.Controls[0];
            }
        }

        #endregion
    }
}
