using System;
using System.IO;
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

using System.Web;
using System.Net;
using System.Reflection;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Controls;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Core.Internal.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using ArcGIS.Desktop.Internal.Mapping.Symbology;

namespace Ag_Analytics_Toolbar.ProfitLayer
{
    internal class ProfitLayerDockpaneViewModel : DockPane, IDataErrorInfo
    {
        private const string _dockPaneID = "ProfitLayerDockpane";
        
        private string validationInputError = null;
        private string _validationSubmitError = null;
        
        private ObservableCollection<RasterLayer> _operationRasterLayers = new ObservableCollection<RasterLayer>();
        private RasterLayer _selectedOperationRasterLayer = null;

        private double _operationRasterCost = 0;

        private readonly ICommand _addOperationRasterCommand;
        public ICommand AddOperationRasterCommand => _addOperationRasterCommand;

        private readonly ICommand _removeOperationRasterCommand;
        public ICommand RemoveOperationRasterCommand => _removeOperationRasterCommand;

        private ObservableCollection<OperationRaster> _operationRasters = new ObservableCollection<OperationRaster>();
        private OperationRaster _selectedOperationRaster = null;

        private bool _checkVarietyRasterLayer = false;
        private ObservableCollection<RasterLayer> _varietyRasterLayers = new ObservableCollection<RasterLayer>();
        private RasterLayer _selectedVarietyRasterLayer = null;

        private bool _checkVarietyDBFTable = false;
        private ObservableCollection<StandaloneTable> _varietyDBFTables = new ObservableCollection<StandaloneTable>();
        private StandaloneTable _selectedVarietyDBFTable = null;

        private int _constantAdd = 0;
        private double _cellSize = 0.0001;

        private string _downloadPath = null;
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

        private string _submitRasters = "";
        private string _submitConstantsVector = "";
        private string _submitCategoryRaster = "";
        private string _submitDbfFile = "";
        private string _submitConstantAdd = "";
        private string _submitCellSize = "";
        private string _submitToken = "";
        private string _submitDownloadFolder = "";

        private bool _submitButtonEnabled = true;

        protected ProfitLayerDockpaneViewModel()
        {
            _addOperationRasterCommand = new RelayCommand(() => AddOperationRasterExecute(), () => true);
            _removeOperationRasterCommand = new RelayCommand(() => RemoveOperationRasterExecute(), () => true);

            _downloadPathCommand = new RelayCommand(() => DownloadPathExecute(), () => true);

            _submitCommand = new RelayCommand(() => SubmitExecute(), () => true);
            _cancelCommand = new RelayCommand(() => CancelExecute(), () => true);

            SetRasterLayers();
            SetStandaloneTables();

        }

        protected override Task InitializeAsync()
        {
            ProjectItemsChangedEvent.Subscribe(OnProjectCollectionChanged);
            
            ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChangedEvent);
            
            LayersAddedEvent.Subscribe(OnLayersAddedEvent);
            LayersRemovedEvent.Subscribe(OnLayersRemovedEvent);
            
            StandaloneTablesAddedEvent.Subscribe(OnStandaloneTablesAddedEvent);
            StandaloneTablesRemovedEvent.Subscribe(OnStandaloneTablesRemovedEvent);
            
            return base.InitializeAsync();
        }

        private void OnProjectCollectionChanged(ProjectItemsChangedEventArgs args)
        {
            SetRasterLayers();
            SetStandaloneTables();

            DownloadPath = Project.Current.DefaultGeodatabasePath;
        }

        private void OnActiveMapViewChangedEvent(ActiveMapViewChangedEventArgs obj)
        {

            if (obj.IncomingView == null)
            {
                // there is no active map view - disable the UI
                return;
            }

            OperationRasterLayers = null;
            SelectedOperationRasterLayer = null;

            VarietyRasterLayers = null;
            SelectedVarietyRasterLayer = null;

            VarietyDBFTables = null;
            SelectedVarietyDBFTable = null;

            // we have an active map view - enable the UI
            SetRasterLayers();
            SetStandaloneTables();

        }
        private void OnLayersAddedEvent(LayerEventsArgs args)
        {
            bool flag = false;
            foreach (var layer in args.Layers)
            {
                if (layer is RasterLayer)
                {
                    flag = true;
                }
            }
            if (flag)
            {
                SetRasterLayers();
            }
        }

        private void OnLayersRemovedEvent(LayerEventsArgs args)
        {
            bool flag = false;
            foreach (var layer in args.Layers)
            {
                if (layer is RasterLayer)
                {
                    flag = true;
                }
            }
            if (flag)
            {
                SetRasterLayers();
            }
        }

        private void OnStandaloneTablesAddedEvent(StandaloneTableEventArgs args)
        {
            SetStandaloneTables();
        }

        private void OnStandaloneTablesRemovedEvent(StandaloneTableEventArgs args)
        {
            SetStandaloneTables();
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

        protected override void OnHelpRequested()
        {
            //System.Diagnostics.Process.Start(@"");
        }

        public void CancelExecute()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Hide();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "ProfitLayer Parameters";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

        public ObservableCollection<RasterLayer> OperationRasterLayers
        {
            get { return _operationRasterLayers; }
            set
            {
                SetProperty(ref _operationRasterLayers, value, () => OperationRasterLayers);
            }
        }

        public RasterLayer SelectedOperationRasterLayer
        {
            get { return _selectedOperationRasterLayer; }
            set
            {
                SetProperty(ref _selectedOperationRasterLayer, value, () => SelectedOperationRasterLayer);
            }
        }

        public double OperationRasterCost
        {
            get { return _operationRasterCost; }
            set
            {
                SetProperty(ref _operationRasterCost, value, () => OperationRasterCost);
            }
        }

        public ObservableCollection<OperationRaster> OperationRasters
        {
            get { return _operationRasters; }
            set
            {
                SetProperty(ref _operationRasters, value, () => OperationRasters);
            }
        }

        public OperationRaster SelectedOperationRaster
        {
            get { return _selectedOperationRaster; }
            set
            {
                SetProperty(ref _selectedOperationRaster, value, () => SelectedOperationRaster);
                if(value != null)
                {
                    SelectedOperationRasterLayer = value.Raster_Layer;
                    OperationRasterCost = value.Raster_Cost;
                }
            }
        }
        
        public bool CheckVarietyRasterLayer
        {
            get { return _checkVarietyRasterLayer; }
            set
            {
                SetProperty(ref _checkVarietyRasterLayer, value, () => CheckVarietyRasterLayer);
            }
        }

        public ObservableCollection<RasterLayer> VarietyRasterLayers
        {
            get { return _varietyRasterLayers; }
            set
            {
                SetProperty(ref _varietyRasterLayers, value, () => VarietyRasterLayers);
            }
        }
        
        public RasterLayer SelectedVarietyRasterLayer
        {
            get { return _selectedVarietyRasterLayer; }
            set
            {
                SetProperty(ref _selectedVarietyRasterLayer, value, () => SelectedVarietyRasterLayer);
            }
        }

        public bool CheckVarietyDBFTable
        {
            get { return _checkVarietyDBFTable; }
            set
            {
                SetProperty(ref _checkVarietyDBFTable, value, () => CheckVarietyDBFTable);
            }
        }

        public ObservableCollection<StandaloneTable> VarietyDBFTables
        {
            get { return _varietyDBFTables; }
            set
            {
                SetProperty(ref _varietyDBFTables, value, () => VarietyDBFTables);
            }
        }
        
        public StandaloneTable SelectedVarietyDBFTable
        {
            get { return _selectedVarietyDBFTable; }
            set
            {
                SetProperty(ref _selectedVarietyDBFTable, value, () => SelectedVarietyDBFTable);
            }
        }

        public int ConstantAdd
        {
            get { return _constantAdd; }
            set
            {
                SetProperty(ref _constantAdd, value, () => ConstantAdd);
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

        public string SubmitRasters
        {
            get { return _submitRasters; }
            set
            {
                SetProperty(ref _submitRasters, value, () => SubmitRasters);
            }
        }

        public string SubmitConstantsVector
        {
            get { return _submitConstantsVector; }
            set
            {
                SetProperty(ref _submitConstantsVector, value, () => SubmitConstantsVector);
            }
        }

        public string SubmitCategoryRaster
        {
            get { return _submitCategoryRaster; }
            set
            {
                SetProperty(ref _submitCategoryRaster, value, () => SubmitCategoryRaster);
            }
        }

        public string SubmitDbfFile
        {
            get { return _submitDbfFile; }
            set
            {
                SetProperty(ref _submitDbfFile, value, () => SubmitDbfFile);
            }
        }

        public string SubmitConstantAdd
        {
            get { return _submitConstantAdd; }
            set
            {
                SetProperty(ref _submitConstantAdd, value, () => SubmitConstantAdd);
            }
        }

        public string SubmitCellSize
        {
            get { return _submitCellSize; }
            set
            {
                SetProperty(ref _submitCellSize, value, () => SubmitCellSize);
            }
        }

        public string SubmitToken
        {
            get { return _submitToken; }
            set
            {
                SetProperty(ref _submitToken, value, () => SubmitToken);
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
                        if (CellSize < 0.00001 || CellSize > 0.0001)
                        {
                            validationInputError = "This value must be between 0.00001 and 0.0001.";
                        }
                        break;
                    default:
                        break;
                }
                return validationInputError;
            }
        }

        public void AddOperationRasterExecute()
        {
            if(_selectedOperationRasterLayer == null)
            {
                return;
            }
            else
            {
                OperationRaster newOperationRaster = new OperationRaster();
                newOperationRaster.Raster_Name = _selectedOperationRasterLayer.Name;
                newOperationRaster.Raster_Cost = _operationRasterCost;
                newOperationRaster.Raster_Layer = _selectedOperationRasterLayer;

                OperationRasters.Add(newOperationRaster);
            }
        }
        
        public void RemoveOperationRasterExecute()
        {
            if(_selectedOperationRaster != null)
            {
                OperationRasters.Remove(_selectedOperationRaster);
            }
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

            if (ok == true)
            {
                var item = dlg.Items.First();

                DownloadPath = item.Path;
            }
        }

        public async void SubmitExecute()
        {
            ValidationSubmitError = null;
            List<string> validationSubmitErrors = new List<string>();
            
            if(_operationRasters.Count == 0)
            {
                validationSubmitErrors.Add("Operation Rasters must be added.");
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
            ProgressMessage = "Iinitalizing...";
            DateTime started_datetime = DateTime.Now;
            SubmitStartedTime = started_datetime.ToString();
            ResultErrorMessage = "";

            string default_path = Path.GetDirectoryName(Project.Current.URI);

            foreach(OperationRaster item in _operationRasters)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string rasterFileName = "Operation_Raster_" + timestamp + ".tif";
                string outputRaster = Path.Combine(default_path, rasterFileName);
                var parameters = Geoprocessing.MakeValueArray(_selectedOperationRasterLayer.Name, outputRaster, "", "", "", false, false, "", false, false, "TIFF");
                IGPResult result = await Geoprocessing.ExecuteToolAsync("management.CopyRaster", parameters, null, null, null, GPExecuteToolFlags.None);
                
                item.Raster_Path = outputRaster;
            }
            
            string outputVarietyRasterPath = null;
            if (_checkVarietyRasterLayer && _selectedVarietyRasterLayer != null)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string rasterFileName = "Raster_" + timestamp + ".tif";
                outputVarietyRasterPath = Path.Combine(default_path, rasterFileName);
                var parameters = Geoprocessing.MakeValueArray(_selectedVarietyRasterLayer.Name, outputVarietyRasterPath, "", "", "", false, false, "", false, false, "TIFF");
                IGPResult result = await Geoprocessing.ExecuteToolAsync("management.CopyRaster", parameters, null, null, null, GPExecuteToolFlags.None);
            }

            string outputVarietyTablePath = null;
            if (_checkVarietyDBFTable && _selectedVarietyDBFTable != null)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string outputTableName = "Table_" + timestamp + ".dbf";
                outputVarietyTablePath = Path.Combine(default_path, outputTableName);
                var parameters = Geoprocessing.MakeValueArray(_selectedVarietyDBFTable.Name, outputVarietyTablePath);
                IGPResult result = await Geoprocessing.ExecuteToolAsync("management.CopyRows", parameters, null, null, null, GPExecuteToolFlags.None);
            }
            
            ProgressMessage = "Request Calling...";

            IRestResponse apiResponse = await BackgroundTask.Run<IRestResponse>(() =>
            {
                var client = new RestClient("https://analytics.ag/api/ToolBoxProxy/ProfitLayer");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AlwaysMultipartFormData = true;

                List<string> raster_path_array = new List<string>();
                List<string> raster_cost_array = new List<string>();

                foreach (OperationRaster raster in _operationRasters)
                {
                    request.AddFile("rasters", raster.Raster_Path);
                    raster_path_array.Add(raster.Raster_Path.ToString());
                    raster_cost_array.Add(raster.Raster_Cost.ToString());
                }
                string constants_vector = string.Join(",", raster_cost_array);

                if (outputVarietyRasterPath != null)
                {
                    request.AddFile("category_raster", outputVarietyRasterPath);
                }

                if (outputVarietyTablePath != null)
                {
                    request.AddFile("dbf_file", outputVarietyTablePath);
                }

                request.AddParameter("constants_vector", constants_vector);
                request.AddParameter("constant_add", _constantAdd);
                request.AddParameter("cell_size", _cellSize);
                request.AddParameter("Token", "v4289wyrwIShfgIWQO4DFWawrzf");

                SubmitRasters = string.Join(",", raster_path_array);
                SubmitConstantsVector = constants_vector;
                SubmitCategoryRaster = outputVarietyRasterPath;
                SubmitDbfFile = outputVarietyTablePath;
                SubmitConstantAdd = _constantAdd.ToString();
                SubmitCellSize = _cellSize.ToString();
                SubmitToken = "v4289wyrwIShfgIWQO4DFWawrzf";
                SubmitDownloadFolder = _downloadPath;

                IRestResponse response = client.Execute(request);

                return response;

            }, BackgroundProgressor.None);

            // delect temporary files
            foreach (OperationRaster item in _operationRasters)
            {
                if (File.Exists(item.Raster_Path))
                {
                    string filesToDelete = Path.GetFileNameWithoutExtension(item.Raster_Path) + ".*";
                    string[] fileList = Directory.GetFiles(default_path, filesToDelete);
                    foreach (string file in fileList)
                    {
                        File.Delete(file);
                    }
                }
            }

            if (File.Exists(outputVarietyRasterPath))
            {
                string filesToDelete = Path.GetFileNameWithoutExtension(outputVarietyRasterPath) + ".*";
                string[] fileList = Directory.GetFiles(default_path, filesToDelete);
                foreach (string file in fileList)
                {
                    File.Delete(file);
                }
            }

            if (File.Exists(outputVarietyTablePath))
            {
                string filesToDelete = Path.GetFileNameWithoutExtension(outputVarietyTablePath) + ".*";
                string[] fileList = Directory.GetFiles(default_path, filesToDelete);
                foreach (string file in fileList)
                {
                    File.Delete(file);
                }
            }

            if (!apiResponse.IsSuccessful)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(response.ErrorMessage);
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Result. Please try again");
                ResultErrorMessage += "Response Error\n";
                ResultErrorMessage += apiResponse.ErrorMessage;

                return;
            }

            string content = apiResponse.Content;
            string unescapedJsonString = JsonConvert.DeserializeObject<dynamic>(content);

            dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(unescapedJsonString);

            ProgressMessage = "Downloading file...";
            try
            {
                string filename = jsonData.file;
                await ExportFile(_downloadPath, filename);
            }
            catch (Exception e)
            {
                if (jsonData.GetType().GetProperty("msg") == null)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No Result");
                    ResultErrorMessage += "Download Error\n";
                    ResultErrorMessage += e.Message;
                    DisplayFailed(started_datetime);
                    return;
                }
                else
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(jsonData.msg);
                    ResultErrorMessage += "Download Error\n";
                    ResultErrorMessage += jsonData.msg;
                    DisplayFailed(started_datetime);
                    return;
                }
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
            await BackgroundTask.Run(() => {

                var download_client = new RestClient("https://analytics.ag/api/ToolBoxProxy/ProfitLayer?filename=" + filename);
                download_client.Timeout = -1;
                var download_request = new RestRequest(Method.GET);
                download_request.AlwaysMultipartFormData = true;

                //download_client.DownloadData(request).SaveAs("C:/result.tif");
                byte[] download_response = download_client.DownloadData(download_request);
                File.WriteAllBytes(Path.Combine(download_path, "ProfitLayer_" + filename), download_response);
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
                    string fullPath = Path.Combine(default_path, "ProfitLayer_" + filename);
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
                    await Ag_Analytics_Module.AddLayerToMapAsync(Path.Combine(download_path, "ProfitLayer_" + filename));
                    await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName("ProfitLayer_" + filename, 10, "Bathymetric Scale");
                }
            });
        }

        private void SetRasterLayers()
        {
            try
            {
                if (MapView.Active == null)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("SetAOILayers: map is not active.");
                    return;
                }

                var _map = MapView.Active.Map;

                List<RasterLayer> layers = _map.GetLayersAsFlattenedList().OfType<RasterLayer>().ToList();
                //IReadOnlyList<Layer> layers = _map.GetLayersAsFlattenedList();

                ObservableCollection<RasterLayer> newRasterLayers = new ObservableCollection<RasterLayer>();
                
                foreach (RasterLayer lyr in layers)
                {
                   newRasterLayers.Add(lyr);
                   
                }

                OperationRasterLayers = newRasterLayers;
                VarietyRasterLayers = newRasterLayers;

                if(OperationRasterLayers.Count > 0)
                {
                    if(SelectedOperationRasterLayer == null)
                    {
                        SelectedOperationRasterLayer = OperationRasterLayers.First();
                    }
                }
                else
                {
                    SelectedOperationRasterLayer = null;
                }

                if (VarietyRasterLayers.Count > 0)
                {
                    if(SelectedVarietyRasterLayer == null)
                    {
                        SelectedVarietyRasterLayer = VarietyRasterLayers.First();
                    }
                }
                else
                {
                    SelectedVarietyRasterLayer = null;
                }
            }
            catch (Exception exc)
            {
                // Catch any exception found and display a message box.
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Exception caught while trying to get layers: " + exc.Message);
                return;
            }
        }

        private void SetStandaloneTables()
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

                IReadOnlyList<StandaloneTable> standaloneTables = _map.StandaloneTables;

                ObservableCollection<StandaloneTable> newTables = new ObservableCollection<StandaloneTable>();
                
                foreach (StandaloneTable table in standaloneTables)
                {

                    newTables.Add(table);

                }

                VarietyDBFTables = newTables;

                if (VarietyDBFTables.Count > 0 && SelectedVarietyDBFTable == null)
                {
                    if(SelectedVarietyDBFTable == null)
                    {
                        SelectedVarietyDBFTable = VarietyDBFTables.First();
                    }
                }
                else
                {
                    SelectedVarietyDBFTable = null;
                }
            }
            catch (Exception exc)
            {
                // Catch any exception found and display a message box.
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Exception caught while trying to get layers: " + exc.Message);
                return;
            }
        }

    }

    public class OperationRaster
    {
        public string Raster_Name
        {
            get;
            set;
        }
        public string Raster_Path
        {
            get;
            set;
        }
        public double Raster_Cost
        {
            get;
            set;
        }
        public RasterLayer Raster_Layer
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class ProfitLayerDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            ProfitLayerDockpaneViewModel.Show();
        }
    }
}
