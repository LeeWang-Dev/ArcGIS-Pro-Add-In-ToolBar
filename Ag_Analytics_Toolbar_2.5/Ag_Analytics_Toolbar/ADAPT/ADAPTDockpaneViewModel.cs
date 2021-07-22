﻿using System;
using System.IO;
using System.IO.Compression;
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

using System.Web;
using System.Net;
using System.Reflection;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Controls;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Events;
//using ArcGIS.Desktop.Editing.Controls;
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

namespace Ag_Analytics_Toolbar.ADAPT
{
    internal class ADAPTDockpaneViewModel : DockPane, IDataErrorInfo
    {
        private const string _dockPaneID = "ADAPTDockpane";

        private string validationInputError = null;
        private string _validationSubmitError = null;
        
        private string _ADAPTFilePath = null;
        
        private readonly ICommand _ADAPTFilePathCommand;
        public ICommand ADAPTFilePathCommand => _ADAPTFilePathCommand;

        private ObservableCollection<string> _sourceTypes = new ObservableCollection<string>();
        private string _selectedSourceType = null;

        private ObservableCollection<string> _shapeTypes = new ObservableCollection<string>();
        private string _selectedShapeType = null;

        private int _frequency = 0;
        private int _dayWindow = 3;

        private ObservableCollection<string> _operationTypes = new ObservableCollection<string>();
        private string _selectedOperationType = null;

        private bool _checkRecalculateArea = true;

        private string _downloadPath = null;
        private readonly ICommand _downloadPathCommand;
        public ICommand DownloadPathCommand => _downloadPathCommand;

        private bool _checkRasterizeShapefile = true;
        private double _cellSize = 0.0001;

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

        private string _submitAdaptFile = "";
        private string _submitSourceType = "";
        private string _submitShapeType = "";
        private string _submitDayWindow = "";
        private string _submitFrequency = "";
        private string _submitOperationType = "";
        private string _submitRecalculateArea = "";
        private string _submitToken = "";
        private string _submitDownloadFolder = "";

        private bool _submitButtonEnabled = true;

        protected ADAPTDockpaneViewModel()
        {
            _ADAPTFilePathCommand = new RelayCommand(() => ADAPTFilePathExecute(), () => true);

            _sourceTypes.Add("Climate");
            _sourceTypes.Add("CNH");
            _selectedSourceType = _sourceTypes.First();

            _shapeTypes.Add("Polygon");
            _shapeTypes.Add("Point");
            _selectedShapeType = _shapeTypes.First();

            _operationTypes.Add("Planting");
            _operationTypes.Add("Harvesting");
            _operationTypes.Add("Spraying");
            _selectedOperationType = _operationTypes.First();

            _downloadPathCommand = new RelayCommand(() => DownloadPathExecute(), () => true);

            _submitCommand = new RelayCommand(() => SubmitExecute(), () => true);
            _cancelCommand = new RelayCommand(() => CancelExecute(), () => true);
        }

        protected override Task InitializeAsync()
        {

            ProjectItemsChangedEvent.Subscribe(OnProjectCollectionChanged);

            return base.InitializeAsync();
        }
        
        private void OnProjectCollectionChanged(ProjectItemsChangedEventArgs args)
        {
            //DownloadPath = Path.GetDirectoryName(Project.Current.URI);
            DownloadPath = Project.Current.DefaultGeodatabasePath;
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
        private string _heading = "ADAPT Parameters";
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
            //System.Diagnostics.Process.Start(@"");
        }

        public void CancelExecute()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Hide();
        }

        public string ADAPTFilePath
        {
            get { return _ADAPTFilePath; }
            set
            {
                SetProperty(ref _ADAPTFilePath, value, () => ADAPTFilePath);
            }
        }

        public void ADAPTFilePathExecute()
        {
            //Display the filter in an Open Item dialog
            BrowseProjectFilter bf = new BrowseProjectFilter("esri_browseDialogFilters_browseFiles");
            bf.Name = "Zipped File";
            bf.FileExtension = "*.zip";
            bf.BrowsingFilesMode = true;

            OpenItemDialog dlg = new OpenItemDialog
            {
                Title = "Open Zipped File",
                InitialLocation = _ADAPTFilePath,
                AlwaysUseInitialLocation = true,
                MultiSelect = false,
                BrowseFilter = bf
            };

            bool? ok = dlg.ShowDialog();

            if (ok == true)
            {
                var item = dlg.Items.First();

                ADAPTFilePath = item.Path;
            }
        }

        public ObservableCollection<string> SourceTypes
        {
            get { return _sourceTypes; }
            set
            {
                SetProperty(ref _sourceTypes, value, () => SourceTypes);
            }
        }

        public string SelectedSourceType
        {
            get { return _selectedSourceType; }
            set
            {
                SetProperty(ref _selectedSourceType, value, () => SelectedSourceType);
            }
        }

        public ObservableCollection<string> ShapeTypes
        {
            get { return _shapeTypes; }
            set
            {
                SetProperty(ref _shapeTypes, value, () => ShapeTypes);
            }
        }

        public string SelectedShapeType
        {
            get { return _selectedShapeType; }
            set
            {
                SetProperty(ref _selectedShapeType, value, () => SelectedShapeType);
            }
        }

        public int Frequency
        {
            get { return _frequency; }
            set
            {
                SetProperty(ref _frequency, value, () => Frequency);
            }
        }

        public int DayWindow
        {
            get { return _dayWindow; }
            set
            {
                SetProperty(ref _dayWindow, value, () => DayWindow);
            }
        }

        public ObservableCollection<string> OperationTypes
        {
            get { return _operationTypes; }
            set
            {
                SetProperty(ref _operationTypes, value, () => OperationTypes);
            }
        }

        public string SelectedOperationType
        {
            get { return _selectedOperationType; }
            set
            {
                SetProperty(ref _selectedOperationType, value, () => SelectedOperationType);
            }
        }

        public bool CheckRecalculateArea
        {
            get { return _checkRecalculateArea; }
            set
            {
                SetProperty(ref _checkRecalculateArea, value, () => CheckRecalculateArea);
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

        public string SubmitAdaptFile
        {
            get { return _submitAdaptFile; }
            set
            {
                SetProperty(ref _submitAdaptFile, value, () => SubmitAdaptFile);
            }
        }

        public string SubmitSourceType
        {
            get { return _submitSourceType; }
            set
            {
                SetProperty(ref _submitSourceType, value, () => SubmitSourceType);
            }
        }

        public string SubmitShapeType
        {
            get { return _submitShapeType; }
            set
            {
                SetProperty(ref _submitShapeType, value, () => SubmitShapeType);
            }
        }

        public string SubmitDayWindow
        {
            get { return _submitDayWindow; }
            set
            {
                SetProperty(ref _submitDayWindow, value, () => SubmitDayWindow);
            }
        }

        public string SubmitFrequency
        {
            get { return _submitFrequency; }
            set
            {
                SetProperty(ref _submitFrequency, value, () => SubmitFrequency);
            }
        }

        public string SubmitOperationType
        {
            get { return _submitOperationType; }
            set
            {
                SetProperty(ref _submitOperationType, value, () => SubmitOperationType);
            }
        }

        public string SubmitRecalculateArea
        {
            get { return _submitRecalculateArea; }
            set
            {
                SetProperty(ref _submitRecalculateArea, value, () => SubmitRecalculateArea);
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
        
        public bool CheckRasterizeShapefile
        {
            get { return _checkRasterizeShapefile; }
            set
            {
                SetProperty(ref _checkRasterizeShapefile, value, () => CheckRasterizeShapefile);
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

        public async Task SubmitExecute()
        {
            ValidationSubmitError = null;

            List<string> validationSubmitErrors = new List<string>();

            if (_ADAPTFilePath == null || string.IsNullOrEmpty(_ADAPTFilePath))
            {
                validationSubmitErrors.Add("ADAPT File must be selected.");
            }
            else
            {
                if (!File.Exists(_ADAPTFilePath))
                {
                    validationSubmitErrors.Add("ADAPT File doesn't exsist.");
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

            IRestResponse apiResponse = await BackgroundTask.Run<IRestResponse>(() => {
                var client = new RestClient("https://analytics.ag/api/ToolBoxProxy/ProcessAdapt");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AlwaysMultipartFormData = true;
                request.AddFile("ADAPT_File", _ADAPTFilePath);
                request.AddParameter("Source_Type", _selectedSourceType);
                request.AddParameter("Shape_Type", _selectedShapeType);
                request.AddParameter("Day_Window", _dayWindow);
                request.AddParameter("Frequency", _frequency);
                request.AddParameter("Operation_Type", _selectedOperationType);
                request.AddParameter("Recalculate_Area", _checkRecalculateArea);
                request.AddParameter("Token", "v4289wyrwIShfgIWQO4DFWawrzf");

                SubmitAdaptFile = _ADAPTFilePath;
                SubmitSourceType = _selectedSourceType;
                SubmitShapeType = _selectedShapeType;
                SubmitDayWindow = _dayWindow.ToString();
                SubmitFrequency = _frequency.ToString();
                SubmitOperationType = _selectedOperationType;
                SubmitRecalculateArea = _checkRecalculateArea.ToString();
                SubmitToken = "v4289wyrwIShfgIWQO4DFWawrzf";
                SubmitDownloadFolder = _downloadPath;

                IRestResponse response = client.Execute(request);

                return response;

            }, BackgroundProgressor.None);

            if (!apiResponse.IsSuccessful)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(response.ErrorMessage);
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Result. Please try again.");
                ResultErrorMessage += "Response Error\n";
                ResultErrorMessage += apiResponse.ErrorMessage;
                DisplayFailed(started_datetime);

                return;
            }
            
            string content = apiResponse.Content;
            string json_string = content.Replace(@"\n", string.Empty).Replace(@"\", string.Empty).Trim('"');

            dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(json_string);

            ProgressMessage = "Downloading files...";

            try
            {
                //System.Diagnostics.Debug.WriteLine();    

                string filename = null;
                string shapefileUrl = null;
                if (jsonData.appliedSummary != null)
                {
                    shapefileUrl = jsonData.appliedSummary[0].ShapeFilePathUrl;
                    filename = jsonData.appliedSummary[0].ID;
                }
                else if (jsonData.harvestSummary != null)
                {
                    shapefileUrl = jsonData.harvestSummary[0].ShapeFilePathUrl;
                    filename = jsonData.harvestSummary[0].ID;
                }
                else if (jsonData.plantingSummary != null)
                {
                    shapefileUrl = jsonData.plantingSummary[0].ShapeFilePathUrl;
                    filename = jsonData.plantingSummary[0].ID;
                }

                if (shapefileUrl == null)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("shapefile url doesn't exsist ");
                    ResultErrorMessage += "\nDownload Error: shapefile url doesn't exsist";
                    DisplayFailed(started_datetime);

                    return;
                }
                else
                {
                    await ExportFile(_downloadPath, filename, shapefileUrl);
                }
            }
            catch (Exception e)
            {
                //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No Result");
                ResultErrorMessage += "\nDownload Error";
                ResultErrorMessage += e.Message;
                DisplayFailed(started_datetime);

                return;
            }

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

        private async Task DownloadZipFile(string download_path, string filename, string shapefileUrl)
        {
            await BackgroundTask.Run(() => {

                var download_client = new RestClient("https://analytics.ag/api/ToolBoxProxy/ProcessAdapt/" + shapefileUrl);
                download_client.Timeout = -1;
                var download_request = new RestRequest(Method.GET);
                download_request.AlwaysMultipartFormData = true;
                
                byte[] download_response = download_client.DownloadData(download_request);
                File.WriteAllBytes(Path.Combine(download_path, filename + ".zip"), download_response);
            }, BackgroundProgressor.None);

            ZipFile.ExtractToDirectory(Path.Combine(download_path, filename + ".zip"), download_path);
        }

        private async Task ExportFile(string download_path, string filename, string shapefileUrl)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string dir_name = filename + "_" + timestamp;

            await QueuedTask.Run(async () => {

                try
                {
                    Geodatabase gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(@download_path)));
                    string default_path = Path.GetDirectoryName(Project.Current.URI);
                    string dir_path = Path.Combine(default_path, dir_name);
                    Directory.CreateDirectory(dir_path);
                    await DownloadZipFile(dir_path, filename, shapefileUrl);
                    string featureClassName = Regex.Replace(filename, @"[^0-9a-zA-Z_]", "_") + "_" + timestamp;  //string.Empty
                    string outputUrl = Path.Combine(download_path, featureClassName);
                    await Ag_Analytics_Module.CopyFeatures(Path.Combine(dir_path, filename+".shp"), outputUrl);

                    if (_checkRasterizeShapefile)
                    {
                        ProgressMessage = "Rasterize...";
                        dynamic json = await RasterizeShapefile(dir_path, filename);

                        if (json == null)
                        {
                            //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Rasterize Shapefile. Please try again.");
                            ResultErrorMessage += "\nFailed Rasterize Shapefile";
                            return;
                        }
                        try
                        {
                            foreach (dynamic item in json)
                            {
                                foreach (string fileUrl in item.raster)
                                {
                                    string ext = Path.GetExtension(fileUrl);
                                    string downloadFileName = filename + "_" + item.field + ext;
                                    await DownloadRasterizeFile(dir_path, downloadFileName, fileUrl);

                                    if (ext == ".tif")
                                    {
                                        string rasterFileName = filename + "_" + item.field + "_" + timestamp;
                                        string rasterName = Regex.Replace(rasterFileName, @"[^0-9a-zA-Z_]", "_");  //string.Empty
                                        string outputRaster = Path.Combine(download_path, rasterName);
                                        await Ag_Analytics_Module.CopyRaster(Path.Combine(dir_path, downloadFileName), outputRaster);
                                        await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName(rasterName, 10, "Bathymetric Scale");
                                    }
                                    else if(ext == ".dbf")
                                    {
                                        string tableFileName = filename + "_" + item.field + "_Table_" + timestamp;
                                        string tableName = Regex.Replace(tableFileName, @"[^0-9a-zA-Z_]", "_");  //string.Empty
                                        await Ag_Analytics_Module.CopyTable(Path.Combine(dir_path, downloadFileName), download_path, tableName);
                                    }
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Rasterize Shapefile. Please try again.");
                            ResultErrorMessage += "\nFailed Rasterize Shapefile:";
                            ResultErrorMessage += e.Message;
                            return;
                        }
                    }
                }
                catch
                {
                    string dir_path = Path.Combine(_downloadPath, dir_name);
                    Directory.CreateDirectory(dir_path);

                    await DownloadZipFile(dir_path, filename, shapefileUrl);

                    await Ag_Analytics_Module.AddLayerToMapAsync(Path.Combine(dir_path, filename + ".shp"));

                    if (_checkRasterizeShapefile)
                    {
                        ProgressMessage = "Rasterize...";

                        dynamic json = await RasterizeShapefile(dir_path, filename);

                        if(json == null)
                        {
                            //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Rasterize Shapefile. Please try again.");
                            ResultErrorMessage += "\nFailed Rasterize Shapefile";
                            return;
                        }
                        try
                        {
                            foreach (dynamic item in json)
                            {
                                foreach (string fileUrl in item.raster)
                                {
                                    string ext = Path.GetExtension(fileUrl);
                                    string downloadFileName = filename + "_" + item.field + ext;
                                    await DownloadRasterizeFile(dir_path, downloadFileName, fileUrl);
                                    if (ext == ".tif")
                                    {
                                        await Ag_Analytics_Module.AddLayerToMapAsync(Path.Combine(dir_path, downloadFileName));
                                        await Ag_Analytics_Module.SetToClassifyColorizerFromLayerName(downloadFileName, 10, "Bathymetric Scale");
                                    }
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Rasterize Shapefile. Please try again.");
                            ResultErrorMessage += "\nFailed Rasterize Shapefile:";
                            ResultErrorMessage += e.Message;
                            return;
                        }
                    }
                }
            });
        }

        private async Task<dynamic> RasterizeShapefile(string download_path, string filename)
        {
            string shapefiles_zip_names = filename + ".shp";

            string shapefileszip = Path.Combine(download_path, filename + ".zip");

            List<string> cell_size_array = new List<string>();
            for (int i = 0; i < 7; i++)
            {
                cell_size_array.Add(_cellSize.ToString());
            }

            string cell_size = string.Join(",", cell_size_array);

            return  await BackgroundTask.Run(() =>
            {
                var client = new RestClient("https://analytics.ag/api/ToolBoxProxy/GIS");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AlwaysMultipartFormData = true;

                request.AddParameter("fieldnames", "AppliedRate,TargetRate,vrYieldVol,WetMass,Moisture,Elevation,Variety");
                request.AddParameter("variety", "0,0,0,0,0,0,1");
                request.AddParameter("shapefiles_zip_names", shapefiles_zip_names);
                request.AddParameter("operation", "shapetorasterV2");
                request.AddParameter("cell_size", cell_size);
                request.AddFile("shapefileszip", shapefileszip);

                request.AddParameter("Token", "v4289wyrwIShfgIWQO4DFWawrzf");
                
                IRestResponse response = client.Execute(request);

                string content = response.Content;
                string json_string = content.Replace(@"\", string.Empty).Trim('"');

                dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(json_string);

                if (!response.IsSuccessful)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed Rasterize Shapefile");
                    return null;
                }

                return jsonData;

            }, BackgroundProgressor.None);
        }

        private async Task DownloadRasterizeFile(string download_path, string filename, string fileUrl)
        {
            await BackgroundTask.Run(() => {

                var download_client = new RestClient("https://analytics.ag/api/ToolBoxProxy/GIS/?filename=" + fileUrl);
                download_client.Timeout = -1;
                var download_request = new RestRequest(Method.GET);
                download_request.AlwaysMultipartFormData = true;

                byte[] download_response = download_client.DownloadData(download_request);
                File.WriteAllBytes(Path.Combine(download_path, filename), download_response);
            }, BackgroundProgressor.None);
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
                    case "Frequency":
                        if(Frequency < 0)
                        {
                            validationInputError = "This value must be > 0.";
                        }
                        break;
                    case "DayWindow":
                        if (DayWindow < 0)
                        {
                            validationInputError = "This value must be > 0.";
                        }
                        break;
                    case "CellSize":
                        if (CellSize < 0.00001 || CellSize > 0.01)
                        {
                            validationInputError = "This value must be between 0.00001 and 0.01.";
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
    internal class ADAPTDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            ADAPTDockpaneViewModel.Show();
        }
    }
}
