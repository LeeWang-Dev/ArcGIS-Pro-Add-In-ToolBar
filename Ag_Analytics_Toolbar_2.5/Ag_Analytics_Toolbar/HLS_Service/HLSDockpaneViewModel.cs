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
//using System.Windows.Controls;

using System.IO;
using System.Web;
using System.Net;
using System.Reflection;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
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

using Ag_Analytics_Toolbar.CoordinateSystemDialog;


namespace Ag_Analytics_Toolbar.HLS_Service
{
    internal class HLSDockpaneViewModel : DockPane, IDataErrorInfo
    {
        private const string _dockPaneID = "HLSDockpane";
        
        //private readonly object _lockCollection = new object();
        
        private string validationInputError = null;
        private string _validationSubmitError = null;

        private ObservableCollection<Layer> _AOILayers = new ObservableCollection<Layer>();
        private Layer _selectedAOILayer = null;
        
        private ObservableCollection<HLS_Band> _bands = new ObservableCollection<HLS_Band>();

        private ObservableCollection<string> _satellites = new ObservableCollection<string>();
        private string _selectedSatellite = null;

        private bool _showLatest = true;
        private bool _dateEnabled = false;
        
        private DateTime _startDate = DateTime.Now;
        private DateTime _endDate = DateTime.Now;

        private double _cellSize = 0.0001;
        private float _qacloudperc = 100;
        private float _displaynormalvalues = 2000;

        private bool _checkbyweek = true;
        private bool _checkfilter = false;
        private bool _checkqafilter = false;
        private bool _checkflattendata = false;       

        private SpatialReference selectedSpatialReference =  null;
        private string _coordinateSystem = null;
        private string _downloadPath = null;

        private readonly ICommand _zoomToLayerCommand;
        public ICommand ZoomToLayerCommand => _zoomToLayerCommand;

        private CoordSysDialog _dlg = null;
        private static bool _isOpen = false;
        private readonly ICommand _openCoordinateSystemCommand;
        public ICommand OpenCoordinateSystemCommand => _openCoordinateSystemCommand;

        private readonly ICommand _downloadPathCommand;
        public ICommand DownloadPathCommand => _downloadPathCommand;

        private readonly ICommand _submitCommand;
        public ICommand SubmitCommand => _submitCommand;
        private readonly ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand;

        private string _progressMessage = "";
        private string _progressVisible = "Hidden";

        private string _resultBoxVisible = "Hidden";
        private string _resultBoxBGColor = "#3a593a";
        private string _resultBoxImage = "pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/DataReviewerLifecycleVerified32.png";
        private string _resultBoxMessage = "Completed";
        private string _submitStartedTime = "";
        private string _completedTime = "";
        private string _elapsedTime = "";
        private string _resultErrorMessage = "";

        private string _submitAOI = "";
        private string _submitBand = "";
        private string _submitSatellite = "";
        private string _submitShowLatest = "";
        private string _submitStartDate = "";
        private string _submitEndDate = "";
        private string _submitResolution = "";
        private string _submitDisplayNormalValues = "";
        private string _submitQacloudperc = "";
        private string _submitByWeek = "";
        private string _submitFilter = "";
        private string _submitQaFilter = "";
        private string _submitFlattenData = "";
        private string _submitProjection = "";
        private string _submitDownloadFolder = "";

        private bool _submitButtonEnabled = true;

        public HLSDockpaneViewModel() {

            string[] band_names = { 
                "Red","Green","Blue","NIR","NIR_Broad","Red_Edge_1","Red_Edge_2","Red_edge_3","SWIR1","SWIR2","Coastal Aerosol","QA",
                "NDVI", "RGB","NDWI","NDBI","NDTI","UI","GCVI","MTCI","NDRE",
                "CIR","UE","LW","AP","AGR","FFBS","BE","VW"
            };
            
            foreach (string band_name in band_names)
            {
                HLS_Band obj = new HLS_Band();
                obj.Band_Name = band_name;
                _bands.Add(obj);
            }

            _satellites.Add("Landsat");
            _satellites.Add("Sentinel");
            _satellites.Add("Landsat, Sentinel");
            _selectedSatellite = _satellites.First();

            _zoomToLayerCommand = new RelayCommand(() => ZoomToLayerExecute(), () => true);
            _openCoordinateSystemCommand = new RelayCommand(() => OpenCoordinateSystemExecute(), () => true);
            _downloadPathCommand = new RelayCommand(() => DownloadPathExecute(), () => true);
            
            _submitCommand = new RelayCommand(() => SubmitExecute(), () => true);
            _cancelCommand = new RelayCommand(() => CancelExecute(), () => true);
            
            SetAOILayers();
            
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
            
            SetAOILayers();
        }

        private void OnLayersRemovedEvent(LayerEventsArgs args)
        {
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
        private string _heading = "HLS Request Parameters";
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
            System.Diagnostics.Process.Start(@"https://ag-analytics.portal.azure-api.net/docs/services/harmonized-landsat-sentinel-service/operations/hls-service");
        }

        public void CancelExecute()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Hide();
        }

        public ObservableCollection<Layer> AOILayers
        {
            get { return _AOILayers; }
            set
            {
                SetProperty(ref _AOILayers, value, () => AOILayers);
            }
            
        }

        public Layer SelectedAOILayer
        {
            get {  return _selectedAOILayer;   }
            
            set
            {
                SetProperty(ref _selectedAOILayer, value, () => SelectedAOILayer);
            }
        }

        public ObservableCollection<HLS_Band> Bands
        {
            get { return _bands; }
            set
            {
                SetProperty(ref _bands, value, () => Bands);
            }
        }
        
        public ObservableCollection<string> Satellites
        {
            get { return _satellites; }
            set
            {
                SetProperty(ref _satellites, value, () => Satellites);
            }
        }

        public string SelectedSatellite
        {
            get { return _selectedSatellite; }
            set
            {
                SetProperty(ref _selectedSatellite, value, () => SelectedSatellite);
            }
        }
        
        public bool ShowLatest
        {
            get { return _showLatest; }
            set
            {
                SetProperty(ref _showLatest, value, () => ShowLatest);
                DateEnabled = !_showLatest;
            }
        }

        public bool DateEnabled
        {
            get { return _dateEnabled; }
            set
            {
                SetProperty(ref _dateEnabled, value, () => DateEnabled);
            }
        }

        public DateTime StartDate
        {
            get { return _startDate; }
            set
            {
                SetProperty(ref _startDate, value, () => StartDate);
            }
        }
        
        public DateTime EndDate
        {
            get { return _endDate; }
            set
            {
                SetProperty(ref _endDate, value, () => EndDate);
            }
        }

        public bool CheckByWeek
        {
            get { return _checkbyweek; }
            set
            {
                SetProperty(ref _checkbyweek, value, () => CheckByWeek);
            }
        }

        public bool CheckFilter
        {
            get { return _checkfilter; }
            set
            {
                SetProperty(ref _checkfilter, value, () => CheckFilter);
            }
        }

        public bool CheckQaFilter
        {
            get { return _checkqafilter; }
            set
            {
                SetProperty(ref _checkqafilter, value, () => CheckQaFilter);
            }
        }

        public bool CheckFlattenData
        {
            get { return _checkflattendata ; }
            set
            {
                SetProperty(ref _checkflattendata, value, () => CheckFlattenData);
            }
        }
        
        public double CellSize
        {
            get { return _cellSize; }
            set
            {
                SetProperty(ref _cellSize, value, () => CellSize);
            }
        }


        // What this logic below? why compare value with CellSize? 
        public float QaCloudPerc
        {
            get { return _qacloudperc; }
            set
            {
                SetProperty(ref _qacloudperc, value, () => QaCloudPerc);
            }
        }

        public float DisplayNormalValues
        {
            get { return _displaynormalvalues; }
            set
            {
                SetProperty(ref _displaynormalvalues, value, () => DisplayNormalValues);
            }
        }

        public string CoordinateSystem
        {
            get { return _coordinateSystem; }
            set
            {
                SetProperty(ref _coordinateSystem, value, () => CoordinateSystem);
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

        public string SubmitAOI
        {
            get { return _submitAOI; }
            set
            {
                SetProperty(ref _submitAOI, value, () => SubmitAOI);
            }
        }

        public string SubmitBand
        {
            get { return _submitBand; }
            set
            {
                SetProperty(ref _submitBand, value, () => SubmitBand);
            }
        }

        public string SubmitSatellite
        {
            get { return _submitSatellite; }
            set
            {
                SetProperty(ref _submitSatellite, value, () => SubmitSatellite);
            }
        }

        public string SubmitShowLatest
        {
            get { return _submitShowLatest; }
            set
            {
                SetProperty(ref _submitShowLatest, value, () => SubmitShowLatest);
            }
        }

        public string SubmitStartDate
        {
            get { return _submitStartDate; }
            set
            {
                SetProperty(ref _submitStartDate, value, () => SubmitStartDate);
            }
        }

        public string SubmitEndDate
        {
            get { return _submitEndDate; }
            set
            {
                SetProperty(ref _submitEndDate, value, () => SubmitEndDate);
            }
        }

        public string SubmitResolution
        {
            get { return _submitResolution; }
            set
            {
                SetProperty(ref _submitResolution, value, () => SubmitResolution);
            }
        }

        public string SubmitDisplayNormalValues
        {
            get { return _submitDisplayNormalValues; }
            set
            {
                SetProperty(ref _submitDisplayNormalValues, value, () => SubmitDisplayNormalValues);
            }
        }

        public string SubmitQacloudperc
        {
            get { return _submitQacloudperc; }
            set
            {
                SetProperty(ref _submitQacloudperc, value, () => SubmitQacloudperc);
            }
        }

        public string SubmitByWeek
        {
            get { return _submitByWeek; }
            set
            {
                SetProperty(ref _submitByWeek, value, () => SubmitByWeek);
            }
        }

        public string SubmitFilter
        {
            get { return _submitFilter; }
            set
            {
                SetProperty(ref _submitFilter, value, () => SubmitFilter);
            }
        }

        public string SubmitQaFilter
        {
            get { return _submitQaFilter; }
            set
            {
                SetProperty(ref _submitQaFilter, value, () => SubmitQaFilter);
            }
        }

        public string SubmitFlattenData
        {
            get { return _submitFlattenData; }
            set
            {
                SetProperty(ref _submitFlattenData, value, () => SubmitFlattenData);
            }
        }
        
        public string SubmitProjection
        {
            get { return _submitProjection; }
            set
            {
                SetProperty(ref _submitProjection, value, () => SubmitProjection);
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

        public async void ZoomToLayerExecute()
        {
            if(MapView.Active != null)
            {
                if(_selectedAOILayer != null)
                {
                    await MapView.Active.ZoomToAsync(_selectedAOILayer);
                }
            }
        }
       
        public void OpenCoordinateSystemExecute()
        {
            if (_isOpen)
                return;
            _isOpen = true;
            _dlg = new CoordSysDialog();
            _dlg.Closing += bld_Closing;
            _dlg.Owner = FrameworkApplication.Current.MainWindow;
            _dlg.Show();
        }

        private void bld_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_dlg.SpatialReference != null)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(string.Format("You picked {0}", _dlg.SpatialReference.Name), "Pick Coordinate System");
                
                CoordinateSystem = _dlg.SpatialReference.Name;
                selectedSpatialReference = _dlg.SpatialReference;
            }
            _dlg = null;
            _isOpen = false;
        }

        public void DownloadPathExecute()
        {
            //Display the filter in an Open Item dialog
            BrowseProjectFilter bf = new BrowseProjectFilter();
            bf.Name = "Folders and Geodatabases";

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
            
            if(ok == true)
            {
                var item = dlg.Items.First();
                
                DownloadPath = item.Path;
            }
        }

        public async Task SubmitExecute()
        {
            ValidationSubmitError = null;

            List<string> validationSubmitErrors = new List<string>();

            string aoi = null;
            
            if (_selectedAOILayer != null)
            {
                if (_selectedAOILayer is FeatureLayer)
                {
                    FeatureLayer lyr = _selectedAOILayer as FeatureLayer;

                    int featureCount = await QueuedTask.Run(() => { return lyr.GetFeatureClass().GetCount(); });
                    if (featureCount == 0)
                    {
                        validationSubmitErrors.Add("AOI is empty.");
                    }
                    else
                    {
                        aoi = await Ag_Analytics_Module.GetGeoJSONFromFeatureLayer(lyr);
                    }
                } else if (_selectedAOILayer is RasterLayer)
                {
                    RasterLayer lyr = _selectedAOILayer as RasterLayer;
                    string default_path = Path.GetDirectoryName(Project.Current.URI);
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string rasterFileName = "AOI_Raster_" + timestamp + ".tif";
                    string outputRaster = Path.Combine(default_path, rasterFileName);
                    var parameters = Geoprocessing.MakeValueArray(lyr.Name, outputRaster,"","","",false,false,"",false,false,"TIFF");
                    IGPResult result = await Geoprocessing.ExecuteToolAsync("management.CopyRaster", parameters, null, null, null, GPExecuteToolFlags.None);

                    aoi = outputRaster;
                }
            }
            else
            {
                validationSubmitErrors.Add("AOI parameter must be selected.");
            }

            string selectedBands = null;
            List<string> _selectedBands = new List<string>();
            foreach(var band in _bands)
            {
                if(band.Check_Status == true)
                {
                    _selectedBands.Add(band.Band_Name);
                }
            }
            if(_selectedBands.Count > 0)
            {
                selectedBands = JsonConvert.SerializeObject(_selectedBands);
            }
            else
            {
                validationSubmitErrors.Add("Bands must be selected");
            }
            
            //satellite: _selectedSatellite
            int _showLatestValue = _showLatest ? 1 : 0;
            string startDate = String.Format("{0:M/d/yyyy}", _startDate);
            string endDate = String.Format("{0:M/d/yyyy}", _endDate);

            if (!_showLatest)
            {
                if (DateTime.Compare(_startDate, _endDate) >= 0)
                {
                    validationSubmitErrors.Add("Start Date must be earlier than End Date.");
                }
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

            SpatialReference outputSpatialReference = null;
            if (selectedSpatialReference == null)
            {
                outputSpatialReference = await QueuedTask.Run(() => { return _selectedAOILayer.GetSpatialReference(); });
            }
            else
            {
                outputSpatialReference = selectedSpatialReference;
            }

            if (outputSpatialReference.IsGeographic && _cellSize > 1)
            {
                validationSubmitErrors.Add("Resolution must be < 1 in geographic coordinate system(ex:0.0001)");
            }
            else if (outputSpatialReference.IsProjected && _cellSize < 1)
            {
                validationSubmitErrors.Add("Resolution must be > 1 in projected coordinate system(ex:10)");
            }

            if (validationSubmitErrors.Count > 0)
            {
                ValidationSubmitError = string.Join("\n", validationSubmitErrors);
                return;
            }
            if(validationInputError != null)
            {
                return;
            }

            //ProgressDialog progressDialog = new ProgressDialog("Please wait for result response...");
            //progressDialog.Show();
            int byweekValue = _checkbyweek ? 1 : 0;
            int filterValue = _checkfilter ? 1 : 0;
            int qafilterValue = _checkqafilter ? 1 : 0;
            int flattendataValue = _checkflattendata ? 1 : 0;

            SubmitButtonEnabled = false;
            ResultBoxVisible = "Hidden";
            ProgressVisible = "Visible";
            ProgressMessage = "Request Calling...";
            DateTime started_datetime = DateTime.Now;
            SubmitStartedTime = started_datetime.ToString();
            ResultErrorMessage = "";

            IRestResponse apiResponse = await BackgroundTask.Run<IRestResponse>(() =>
            {
                var client = new RestClient("https://ag-analytics.azure-api.net/hls-service/");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                
                //request.AlwaysMultipartFormData = true;
                //request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

                if(_selectedAOILayer is FeatureLayer)
                {
                    request.AddParameter("aoi", aoi);
                }
                else if(_selectedAOILayer is RasterLayer)
                {
                    request.AddFile("aoi", aoi);
                }

                request.AddParameter("Band", selectedBands);
                request.AddParameter("satellite", _selectedSatellite);
               
                request.AddParameter("showlatest", _showLatestValue);
                request.AddParameter("Startdate", startDate);
                request.AddParameter("Enddate", endDate);

                request.AddParameter("resolution", _cellSize);
                request.AddParameter("displaynormalvalues", _displaynormalvalues);
                request.AddParameter("qacloudperc", _qacloudperc);

                request.AddParameter("byweek", byweekValue);
                request.AddParameter("filter", filterValue);
                request.AddParameter("qafilter", qafilterValue);
                request.AddParameter("flatten_data", flattendataValue);
                
                request.AddParameter("projection", outputSpatialReference.Wkt);

                // these parameter options no need on ArcGIS pro
                request.AddParameter("legendtype", "Relative"); 
                request.AddParameter("statistics", 0);  // set always 0
                request.AddParameter("return_tif", 1);  // set always 1

                SubmitAOI = aoi;
                SubmitBand = selectedBands;
                SubmitSatellite = _selectedSatellite;
                SubmitShowLatest = _showLatestValue.ToString();
                SubmitStartDate = startDate;
                SubmitEndDate = endDate;
                SubmitResolution = _cellSize.ToString();
                SubmitDisplayNormalValues = _displaynormalvalues.ToString();
                SubmitQacloudperc = _qacloudperc.ToString();
                SubmitByWeek = byweekValue.ToString();
                SubmitFilter = filterValue.ToString();
                SubmitQaFilter = qafilterValue.ToString();
                SubmitFlattenData = flattendataValue.ToString();
                SubmitProjection = outputSpatialReference.Wkt;
                SubmitDownloadFolder = _downloadPath;

                IRestResponse response = client.Execute(request);
                
                return response;
                
            }, BackgroundProgressor.None);

            if (File.Exists(aoi))
            {
                string default_path = Path.GetDirectoryName(Project.Current.URI);
                string filesToDelete = Path.GetFileNameWithoutExtension(aoi) + ".*";
                string[] fileList = Directory.GetFiles(default_path, filesToDelete);
                foreach (string file in fileList)
                {
                    File.Delete(file);
                }
            }

            if (!apiResponse.IsSuccessful)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(response.ErrorMessage);
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Result. Please try again.");
                ResultErrorMessage += "Response Error\n";
                ResultErrorMessage += apiResponse.ErrorMessage;

                DisplayFailed(started_datetime);

                return;
            }

            dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(apiResponse.Content);

            ProgressMessage = "Downloading tif files...";
            try
            {
                foreach (dynamic item in jsonData)
                {
                    string filename = item.download_url;
                    await ExportFile(_downloadPath, filename);
                }
            }
            catch (Exception e)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("HLS API Error. Please try again.");
                ResultErrorMessage += "Download Error\n";
                ResultErrorMessage += e.Message;

                DisplayFailed(started_datetime);

                return;
            }

            //progressDialog.Hide();
            ProgressVisible = "Hidden";
            ProgressMessage = "";
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
            await BackgroundTask.Run(() => {

                var download_client = new RestClient("https://ag-analytics.azure-api.net/hls-service/?filename=" + filename);
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
                    await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName(rasterName, 10, "Bathymetric Scale");
                    // delete files in default path
                    File.Delete(fullPath);
                }
                catch
                {
                    await DownloadFile(download_path, filename);
                    await Ag_Analytics_Module.AddLayerToMapAsync(Path.Combine(download_path, filename));
                    await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName(filename, 10, "Bathymetric Scale");
                }
            });
        }

        private void SetAOILayers()
        {
            try
            {
                // Gets the first 2D map from the project that is called Map.
                //Map _map = await GetMapFromProject(Project.Current, "Map");
                if(MapView.Active == null)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("SetAOILayers: map is not active.");
                    return;
                }
               
                var _map = MapView.Active.Map;

                //List<Layer> layers = _map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
                IReadOnlyList<Layer> layers = _map.GetLayersAsFlattenedList();
                
                ObservableCollection<Layer> newAOILayers = new ObservableCollection<Layer>();
               
                foreach(Layer lyr in layers)
                {
                    if(lyr is RasterLayer)
                    {
                        newAOILayers.Add(lyr);
                    }
                    else if (lyr is FeatureLayer)
                    {
                        FeatureLayer featureLayer = lyr as FeatureLayer;
                        if (featureLayer.ShapeType == esriGeometryType.esriGeometryPolygon)
                        {
                            newAOILayers.Add(lyr);
                        }
                    }
                }

                AOILayers = newAOILayers;

                if(AOILayers.Count > 0)
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
                    case "CellSize":
                        // check if projection's unit. if is degree, limit is 0.00001, if is meter, limit is 1 m
                        if(CellSize < 0.00001)
                        {
                            validationInputError = "Resolution must be > 0.00001 degreee.";
                        }
                        break;
                    case "QaCloudPerc":
                        if(QaCloudPerc < 0 || QaCloudPerc > 100)
                        {
                            validationInputError = "This value Must be between 0 and 100.";
                        }
                        break;
                    case "DisplayNormalValues":
                        if (DisplayNormalValues < 0) {
                            validationInputError = "This value Must be > 0.";
                        }
                        break;

                    default:
                        break;
                }
                return validationInputError;
            }
        }
    }
    

    public class HLS_Band
    {
        public string Band_Name
        {
            get;
            set;
        }
        public Boolean Check_Status
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class HLSDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            HLSDockpaneViewModel.Show();
            
        }
    }
}
