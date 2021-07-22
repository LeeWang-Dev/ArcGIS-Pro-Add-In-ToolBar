using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using System.IO;
using System.Web;
using System.Net;
using System.Reflection;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Controls;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Core.Internal.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Ag_Analytics_Toolbar.YieldAI
{
    internal class YieldAIDockpaneViewModel : DockPane, IDataErrorInfo
    {
        private const string _dockPaneID = "YieldAIDockpane";

        private string validationInputError = null;
        private string _validationSubmitError = null;

        private ObservableCollection<FeatureLayer> _AOILayers = new ObservableCollection<FeatureLayer>();
        private FeatureLayer _selectedAOILayer = null;

        private ObservableCollection<string> _modelNames = new ObservableCollection<string>();
        private string _selectedModelName = null;

        private ObservableCollection<string> _cropSeasons = new ObservableCollection<string>();
        private string _selectedCropSeason = null;

        private DateTime _plantingDay1 = DateTime.Now;
        private DateTime _harvestDay = DateTime.Now;

        private int _seedingDensity = 30000;

        private string _downloadPath = null;

        private readonly ICommand _zoomToLayerCommand;
        public ICommand ZoomToLayerCommand => _zoomToLayerCommand;

        private readonly ICommand _downloadPathCommand;
        public ICommand DownloadPathCommand => _downloadPathCommand;

        private readonly ICommand _submitCommand;
        public ICommand SubmitCommand => _submitCommand;
        private readonly ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand;

        private string _progressVisible = "Hidden";
        private string _progressMessage = "";

        private string _resultBoxVisible = "Hidden";
        private string _resultBoxBGColor = "#3a593a";
        private string _resultBoxImage = "pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/DataReviewerLifecycleVerified32.png";
        private string _resultBoxMessage = "Completed";
        private string _submitStartedTime = "";
        private string _completedTime = "";
        private string _elapsedTime = "";
        private string _resultErrorMessage = "";

        private string _submitMODELNAME = "";
        private string _submitAOI = "";
        private string _submitScalarVariables = "";
        private string _submitDownloadFolder = "";

        private bool _submitButtonEnabled = true;

        protected YieldAIDockpaneViewModel() {
            string[] model_names = {"NN"};
            foreach (string model_name in model_names)
            {
                _modelNames.Add(model_name);
            }
            _selectedModelName = _modelNames.First();
            for (int i = 2013; i <= DateTime.Now.Year; i++)
            {
                _cropSeasons.Add(i.ToString());
            }
            _selectedCropSeason = _cropSeasons.Last();

            _zoomToLayerCommand = new RelayCommand(() => ZoomToLayerExecute(), () => true);
            _downloadPathCommand = new RelayCommand(() => DownloadPathExecute(), () => true);
            _submitCommand = new RelayCommand(() => SubmitExecute(), () => true);
            _cancelCommand = new RelayCommand(() => CancelExecute(), () => true);

            SetAOILayers();
        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "YieldAI Parameters";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

        protected override void OnHelpRequested()
        {
            System.Diagnostics.Process.Start(@"https://ag-analytics.portal.azure-api.net/docs/services/yield-forecast/operations/yield-model");
        }

        public void CancelExecute()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Hide();
        }

        protected override Task InitializeAsync()
        {
            ProjectItemsChangedEvent.Subscribe(OnProjectCollectionChanged);
            ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChangedEvent);

            LayersAddedEvent.Subscribe(OnLayersAddedEvent);
            LayersRemovedEvent.Subscribe(OnLayersRemovedEvent);

            return base.InitializeAsync();
        }

        private void OnProjectCollectionChanged(ProjectItemsChangedEventArgs args)
        {
            SetAOILayers();
            //DownloadPath = Path.GetDirectoryName(Project.Current.URI);
            DownloadPath = Project.Current.DefaultGeodatabasePath;
        }

        private void OnActiveMapViewChangedEvent(ActiveMapViewChangedEventArgs obj)
        {

            if (obj.IncomingView == null)
            {
                // there is no active map view - disable the UI
                return;
            }
            // we have an active map view - enable the UI
            AOILayers = null;
            SelectedAOILayer = null;
            SetAOILayers();

        }

        private void OnLayersAddedEvent(LayerEventsArgs args)
        {
            bool flag = false;
            foreach (var layer in args.Layers)
            {
                if (layer is FeatureLayer)
                {
                    flag = true;
                }
            }
            if (flag)
            {
                SetAOILayers();
            }
        }

        private void OnLayersRemovedEvent(LayerEventsArgs args)
        {

            bool flag = false;
            foreach (var layer in args.Layers)
            {
                if (layer is FeatureLayer)
                {
                    flag = true;
                }
            }
            if (flag)
            {
                SetAOILayers();
            }
        }

        public async void ZoomToLayerExecute()
        {
            if (MapView.Active != null)
            {
                if (_selectedAOILayer != null)
                {
                    await MapView.Active.ZoomToAsync(_selectedAOILayer);
                }
            }
        }

        public ObservableCollection<FeatureLayer> AOILayers
        {
            get { return _AOILayers; }
            set
            {
                SetProperty(ref _AOILayers, value, () => AOILayers);
            }

        }

        public FeatureLayer SelectedAOILayer
        {
            get { return _selectedAOILayer; }

            set
            {
                SetProperty(ref _selectedAOILayer, value, () => SelectedAOILayer);
            }
        }

        public ObservableCollection<string> ModelNames
        {
            get { return _modelNames; }
            set
            {
                SetProperty(ref _modelNames, value, () => ModelNames);
            }
        }

        public string SelectedModelName
        {
            get { return _selectedModelName; }
            set
            {
                SetProperty(ref _selectedModelName, value, () => SelectedModelName);
            }
        }

        public ObservableCollection<string> CropSeasons
        {
            get { return _cropSeasons; }
            set
            {
                SetProperty(ref _cropSeasons, value, () => CropSeasons);
            }
        }

        public string SelectedCropSeason
        {
            get { return _selectedCropSeason; }
            set
            {
                SetProperty(ref _selectedCropSeason, value, () => SelectedCropSeason);
            }
        }

        public DateTime PlantingDay1
        {
            get { return _plantingDay1; }
            set
            {
                SetProperty(ref _plantingDay1, value, () => PlantingDay1);
            }
        }

        public DateTime HarvestDay
        {
            get { return _harvestDay; }
            set
            {
                SetProperty(ref _harvestDay, value, () => HarvestDay);
            }
        }

        public int SeedingDensity
        {
            get { return _seedingDensity; }
            set
            {
                SetProperty(ref _seedingDensity, value, () => SeedingDensity);
            }
        }

        public string DownloadPath
        {
            get { return _downloadPath; }
            set
            {
                SetProperty(ref _downloadPath, value, () => DownloadPath);
            }
        }

        public string ProgressMessage
        {
            get { return _progressMessage; }
            set
            {
                SetProperty(ref _progressMessage, value, () => ProgressMessage);
            }
        }

        public string ProgressVisible
        {
            get { return _progressVisible; }
            set
            {
                SetProperty(ref _progressVisible, value, () => ProgressVisible);
            }
        }

        public string ResultBoxVisible
        {
            get { return _resultBoxVisible; }
            set
            {
                SetProperty(ref _resultBoxVisible, value, () => ResultBoxVisible);
            }
        }

        public string ResultBoxBGColor
        {
            get { return _resultBoxBGColor; }
            set
            {
                SetProperty(ref _resultBoxBGColor, value, () => ResultBoxBGColor);
            }
        }

        public string ResultBoxImage
        {
            get { return _resultBoxImage; }
            set
            {
                SetProperty(ref _resultBoxImage, value, () => ResultBoxImage);
            }
        }

        public string ResultBoxMessage
        {
            get { return _resultBoxMessage; }
            set
            {
                SetProperty(ref _resultBoxMessage, value, () => ResultBoxMessage);
            }
        }

        public string SubmitStartedTime
        {
            get { return _submitStartedTime; }
            set
            {
                SetProperty(ref _submitStartedTime, value, () => SubmitStartedTime);
            }
        }
        public string CompletedTime
        {
            get { return _completedTime; }
            set
            {
                SetProperty(ref _completedTime, value, () => CompletedTime);
            }
        }

        public string ElapsedTime
        {
            get { return _elapsedTime; }
            set
            {
                SetProperty(ref _elapsedTime, value, () => ElapsedTime);
            }
        }

        public string ResultErrorMessage
        {
            get { return _resultErrorMessage; }
            set
            {
                SetProperty(ref _resultErrorMessage, value, () => ResultErrorMessage);
            }
        }
        
        public string SubmitMODELNAME
        {
            get { return _submitMODELNAME; }
            set
            {
                SetProperty(ref _submitMODELNAME, value, () => SubmitMODELNAME);
            }
        }

        public string SubmitAOI
        {
            get { return _submitAOI; }
            set
            {
                SetProperty(ref _submitAOI, value, () => SubmitAOI);
            }
        }

        public string SubmitScalarVariables
        {
            get { return _submitScalarVariables; }
            set
            {
                SetProperty(ref _submitScalarVariables, value, () => SubmitScalarVariables);
            }
        }

        public string SubmitDownloadFolder
        {
            get { return _submitDownloadFolder; }
            set
            {
                SetProperty(ref _submitDownloadFolder, value, () => SubmitDownloadFolder);
            }
        }

        public bool SubmitButtonEnabled
        {
            get { return _submitButtonEnabled; }
            set
            {
                SetProperty(ref _submitButtonEnabled, value, () => SubmitButtonEnabled);
            }
        }

        public void DownloadPathExecute()
        {
            //Display the filter in an Open Item dialog
            BrowseProjectFilter bf = new BrowseProjectFilter();
            bf.Name = "Folders and Geodatabases ";

            bf.AddFilter(BrowseProjectFilter.GetFilter(ItemFilters.geodatabases));
            bf.AddFilter(BrowseProjectFilter.GetFilter(ItemFilters.folders));

            bf.Includes.Add("FolderConnection");
            bf.Includes.Add("GDB");
            bf.Excludes.Add("esri_browsePlaces_Online");

            OpenItemDialog dlg = new OpenItemDialog
            {
                Title = "Open Folder Dialog",
                InitialLocation = _downloadPath,
                AlwaysUseInitialLocation = true,
                MultiSelect = false,
                BrowseFilter = bf
            };

            bool? ok = dlg.ShowDialog();

            if (ok == true)
            {
                var item = dlg.Items.First();

                DownloadPath = item.Path;
            }
        }

        private void SetAOILayers()
        {
            try
            {
                // Gets the first 2D map from the project that is called Map.
                //Map _map = await GetMapFromProject(Project.Current, "Map");
                if (MapView.Active == null)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("SetAOILayers: map is not active.");
                    return;
                }

                var _map = MapView.Active.Map;

                List<FeatureLayer> featureLayerList = _map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                ObservableCollection<FeatureLayer> newAOILayers = new ObservableCollection<FeatureLayer>();

                foreach (FeatureLayer featureLayer in featureLayerList)
                {
                    if (featureLayer.ShapeType == esriGeometryType.esriGeometryPolygon)
                    {
                        newAOILayers.Add(featureLayer);
                    }
                }
                
                AOILayers = newAOILayers;

                if (AOILayers.Count > 0)
                {
                    if(SelectedAOILayer == null)
                    {
                        SelectedAOILayer = AOILayers.First();
                    }
                }
                else
                {
                    SelectedAOILayer = null;
                }
            }
            catch (Exception exc)
            {
                // Catch any exception found and display a message box.
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Exception caught while trying to get layers: " + exc.Message);
                return;
            }
        }

        public async void SubmitExecute()
        {

            ValidationSubmitError = null;

            List<string> validationSubmitErrors = new List<string>();

            string aoi = null;
            if (_selectedAOILayer != null)
            {
                int featureCount = await QueuedTask.Run(() => { return _selectedAOILayer.GetFeatureClass().GetCount(); });
                if (featureCount == 0)
                {
                    validationSubmitErrors.Add("AOI is empty.");
                }
                else
                {
                    aoi = await Ag_Analytics_Module.GetGeoJSONFromFeatureLayer(_selectedAOILayer);
                }
            }
            else
            {
                validationSubmitErrors.Add("AOI parameter must be selected.");
            }

            if (DateTime.Compare(_plantingDay1, _harvestDay) >= 0)
            {
                validationSubmitErrors.Add("PlantingDay1 must be earlier than HarvestDay.");
            }

            if (_downloadPath == null || string.IsNullOrEmpty(_downloadPath))
            {
                validationSubmitErrors.Add("Download path must be selected.");
            }
            else
            {
                if (!Directory.Exists(_downloadPath))
                {
                    validationSubmitErrors.Add("Download path doesn't exsist.");
                }
            }
            
            if (validationSubmitErrors.Count > 0)
            {
                ValidationSubmitError = string.Join("\n", validationSubmitErrors);
                return;
            }
            if (validationInputError != null)
            {
                return;
            }

            SubmitButtonEnabled = false;
            ResultBoxVisible = "Hidden";
            ProgressVisible = "Visible";
            ProgressMessage = "Request Calling...";
            DateTime started_datetime = DateTime.Now;
            SubmitStartedTime = started_datetime.ToString();
            ResultErrorMessage = "";

            IRestResponse apiResponse = await BackgroundTask.Run<IRestResponse>(()=> {
                var client = new RestClient("https://ag-analytics.azure-api.net/yieldforecast/");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);

                request.AlwaysMultipartFormData = true;
                request.AddHeader("Content-Type", "application/json");
                //request.AddHeader("Ocp-Apim-Subscription-Key", "35520095b0a7408784e712e278d036a7");
                request.AddParameter("MODELNAME", _selectedModelName);
                request.AddParameter("SHAPE", aoi);

                var scalarVariables = new
                {
                    CropSeason = _selectedCropSeason,
                    PlantingDay1 = String.Format("{0:M/d/yyyy}", _plantingDay1),
                    HarvestDay = String.Format("{0:M/d/yyyy}", _harvestDay),
                    SeedingDensity = _seedingDensity
                };

                string scalarVariables_string = JsonConvert.SerializeObject(scalarVariables);

                request.AddParameter("ScalarVariables", scalarVariables_string);

                SubmitMODELNAME = _selectedModelName;
                SubmitAOI = aoi;
                SubmitScalarVariables = scalarVariables_string;
                SubmitDownloadFolder = _downloadPath;

                IRestResponse response = client.Execute(request);

                return response;

            }, BackgroundProgressor.None);

            if (!apiResponse.IsSuccessful)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(response.ErrorMessage);
                ResultErrorMessage += "Response Error\n";
                ResultErrorMessage += apiResponse.ErrorMessage;
                DisplayFailed(started_datetime);
                return;
            }

            dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(apiResponse.Content);

            ProgressMessage = "Downloading file...";

            try
            {
                string filename = jsonData.raster_filename;
                await ExportFile(_downloadPath, filename);
            }
            catch (Exception e)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No Result");
                ResultErrorMessage += "Download Error\n";
                ResultErrorMessage += e.Message;
                DisplayFailed(started_datetime);

                return;
            }

            ProgressVisible = "Hidden";
            DateTime ended_datetime = DateTime.Now;
            CompletedTime = ended_datetime.ToString();
            int seconds = (int)(ended_datetime.Subtract(started_datetime).TotalSeconds);
            ElapsedTime = seconds.ToString() + "  Seconds";
           
            ResultBoxBGColor = "#3a593a";
            ResultBoxImage = "pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/DataReviewerLifecycleVerified32.png";
            ResultBoxMessage = "Completed";
            ResultErrorMessage = "There are no errors or warnings.";
            ResultBoxVisible = "Visible";
            SubmitButtonEnabled = true;
        }

        private void DisplayFailed(DateTime started_datetime)
        {
            DateTime ended_datetime = DateTime.Now;
            CompletedTime = ended_datetime.ToString();
            int seconds = (int)(ended_datetime.Subtract(started_datetime).TotalSeconds);
            ElapsedTime = seconds.ToString() + "  Seconds";

            ProgressVisible = "Hidden";
            ProgressMessage = "";
            ResultBoxBGColor = "#4e2117";
            ResultBoxImage = "pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/close32.png";
            ResultBoxMessage = "Failed";
            ResultBoxVisible = "Visible";
            SubmitButtonEnabled = true;
        }

        private async Task DownloadFile(string download_path, string filename)
        {
            await BackgroundTask.Run(() =>
            {
                var download_client = new RestClient("https://ag-analytics.azure-api.net/yieldforecast/?filename=" + filename);
                download_client.Timeout = -1;
                var download_request = new RestRequest(Method.GET);
                download_request.AlwaysMultipartFormData = true;

                //download_client.DownloadData(request).SaveAs("C:/result.tif");
                byte[] download_response = download_client.DownloadData(download_request);
                File.WriteAllBytes(Path.Combine(download_path, filename), download_response);
            }, BackgroundProgressor.None);
        }

        private async Task ExportFile(string download_path, string filename)
        {
            await QueuedTask.Run(async () => {

                try
                {
                    Geodatabase gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(@download_path)));
                    string default_path = Path.GetDirectoryName(Project.Current.URI);
                    await DownloadFile(default_path, filename);
                    string fullPath = Path.Combine(default_path, filename);
                    string rasterFileName = Path.GetFileNameWithoutExtension(fullPath);
                    string rasterName = Regex.Replace(rasterFileName, @"[^0-9a-zA-Z_]", "_");  //string.Empty
                    string outputRaster = Path.Combine(download_path, rasterName);
                    await Ag_Analytics_Module.CopyRaster(fullPath, outputRaster);
                    await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName(rasterName, 10, "Slope");
                    // delete files in default path
                    File.Delete(fullPath);
                }
                catch
                {
                    await DownloadFile(download_path, filename);
                    await Ag_Analytics_Module.AddLayerToMapAsync(Path.Combine(download_path, filename));
                    await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName(filename, 10, "Slope");
                }
            });
        }

        public string ValidationSubmitError
        {
            get { return _validationSubmitError; }
            set
            {
                SetProperty(ref _validationSubmitError, value, () => ValidationSubmitError);
            }
        }

        public string Error
        {
            get

            {
                return this[string.Empty];
            }
        }

        public string this[string cuurectname]
        {
            get
            {
                validationInputError = null;

                switch (cuurectname)
                {
                    case "SeedingDensity":
                        if (SeedingDensity < 0)
                        {
                            validationInputError = "Sedding Density must be > 0.";
                        }
                        break;
                    default:
                        break;
                }
                return validationInputError;
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class YieldAIDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            YieldAIDockpaneViewModel.Show();
        }
    }
}
