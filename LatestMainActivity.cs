using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Locations;
using Android.Util;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Java.Lang;
using Android.Content.PM;
using System.IO;
using Android.Views.InputMethods;
using System.ComponentModel;
using Newtonsoft.Json;
using GPSCaptureEntity;
using System.Timers;

namespace GPSCapture
{
    [Activity(Label = "TPH Identify", Icon = "@drawable/icon", MainLauncher = true, WindowSoftInputMode = SoftInput.StateHidden, ScreenOrientation = ScreenOrientation.Portrait)]
    public class LatestMainActivity : Activity, ILocationListener
    {
        bool _DoubleBackToExitPressedOnce = false;
        bool _AllData = true;
        bool _getAddress = false;
        int _idx = 0;
        LocationManager _locationManager;

        List<GpsCoordinate> _gpsCoordinates;

        Timer _tmrCounter;

        Spinner _estateSpinner;
        Spinner _afdelingSpinner;
        Spinner _blockSpinner;
        EstateAdapter _estateItems;
        AfdelingAdapter _afdelingItems;
        BlockAdapter _blockItems;

        string _locationProvider;
        EditText _latitudeText;
        EditText _longitudeText;
        EditText _accuracyText;

        ProgressDialog _locationProgress, _processProgress;
        ProgressDialog _downloadProgress, _uploadProgress;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            //Window.RequestFeature(WindowFeatures.NoTitle);
            Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.LatestMain);

            SqliteDatabase.newInstance(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "gps_coordinate.db3"));
            SqliteDatabase.CreateTables();
            //SqliteDatabase.ClearData();

            CacheManager.Init(Application.Context);

            ActionBar mActionBar = this.ActionBar;
            mActionBar.SetDisplayShowHomeEnabled(false);
            mActionBar.SetDisplayShowTitleEnabled(false);
            LayoutInflater mInflater = LayoutInflater.From(this);
            View mCustomView = mInflater.Inflate(Resource.Layout.CustomTitlebar, null);
            mActionBar.DisplayOptions = ActionBarDisplayOptions.ShowCustom;
            mActionBar.CustomView = mCustomView;
            mActionBar.SetDisplayShowCustomEnabled(true);

            FindViewById<Button>(Resource.Id.btnDownload).Click += DownloadButton_OnClick;
            FindViewById<Button>(Resource.Id.btnUpload).Click += UploadButton_OnClick;

            _latitudeText = FindViewById<EditText>(Resource.Id.et_latitude);
            _longitudeText = FindViewById<EditText>(Resource.Id.et_longitude);
            _accuracyText = FindViewById<EditText>(Resource.Id.et_accuracy);
            FindViewById<Button>(Resource.Id.btn_get_coordinates).Click += GetCoordinatesButton_OnClick;
            FindViewById<Button>(Resource.Id.btn_prev).Click += PrevButton_OnClick;
            FindViewById<Button>(Resource.Id.btn_next).Click += NextButton_OnClick;
            FindViewById<Button>(Resource.Id.btn_go).Click += GoButton_OnClick;

            FindViewById<EditText>(Resource.Id.et_tph).TextChanged += TphEditText_TextChanged;

            _locationProgress = new ProgressDialog(this);
            _locationProgress.Indeterminate = true;
            _locationProgress.SetProgressStyle(ProgressDialogStyle.Spinner);
            _locationProgress.SetMessage("Waiting for gps. Please wait...");
            _locationProgress.SetCancelable(false);
            _locationProgress.SetButton("Cancel", (senderAlert, args) =>
            {
                CancelEvent();
            });

            InitializeLocationManager();

            _tmrCounter = new Timer();
            _tmrCounter.Interval = 1000;
            _tmrCounter.Elapsed += tmrCounter_Elapsed;

            _processProgress = new ProgressDialog(this);
            _processProgress.Indeterminate = true;
            _processProgress.SetProgressStyle(ProgressDialogStyle.Spinner);
            _processProgress.SetMessage("Loading...");
            _processProgress.SetCancelable(false);

            _estateSpinner = FindViewById<Spinner>(Resource.Id.spin_estate);
            _afdelingSpinner = FindViewById<Spinner>(Resource.Id.spin_afdeling);
            _blockSpinner = FindViewById<Spinner>(Resource.Id.spin_block);

            try
            {
                Java.Lang.Reflect.Field popup = _blockSpinner.Class.GetDeclaredField("mPopup");
                popup.Accessible = true;

                // Get private mPopup member variable and try cast to ListPopupWindow
                var popupWindow = (ListPopupWindow)popup.Get(_blockSpinner);

                // Set popupWindow height to 300px
                popupWindow.Height = 300;
            }
            catch (Java.Lang.Exception ex)
            {
                Log.Debug("Java Exception", ex.Message);
                // Failed...
            }
            catch (System.Exception ex)
            {
                Log.Debug("System Exception", ex.Message);
                // Failed...
            }

            _estateSpinner.ItemSelected += SpinEstate_ItemSelected;
            _afdelingSpinner.ItemSelected += SpinAfdeling_ItemSelected;
            _blockSpinner.ItemSelected += SpinBlock_ItemSelected;

            PopulateEstate();
        }

        void TphEditText_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {
            FindViewById<Button>(Resource.Id.btn_go).Enabled =
                (FindViewById<EditText>(Resource.Id.et_tph).Text != _gpsCoordinates[_idx].TPH &&
                !string.IsNullOrEmpty(FindViewById<EditText>(Resource.Id.et_tph).Text));

            FindViewById<Button>(Resource.Id.btn_get_coordinates).Enabled = FindViewById<EditText>(Resource.Id.et_tph).Text == _gpsCoordinates[_idx].TPH;
        }

        void SpinEstate_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            _processProgress.Show();
            try
            {
                if (FindViewById<EditText>(Resource.Id.et_remarks).Text != _gpsCoordinates[_idx].Remarks)
                {
                    SaveData(_idx, FindViewById<EditText>(Resource.Id.et_remarks).Text);
                }
            }
            catch { }

            PopulateAfdeling(_estateItems[e.Position]);
            _processProgress.Dismiss();
        }

        void SpinAfdeling_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            _processProgress.Show();
            try
            {
                if (FindViewById<EditText>(Resource.Id.et_remarks).Text != _gpsCoordinates[_idx].Remarks)
                {
                    SaveData(_idx, FindViewById<EditText>(Resource.Id.et_remarks).Text);
                }
            }
            catch { }

            PopulateBlock(_estateSpinner.SelectedItem.ToString(), _afdelingItems[e.Position]);
            _processProgress.Dismiss();
        }

        void SpinBlock_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            _processProgress.Show();
            try
            {
                if (FindViewById<EditText>(Resource.Id.et_remarks).Text != _gpsCoordinates[_idx].Remarks)
                {
                    SaveData(_idx, FindViewById<EditText>(Resource.Id.et_remarks).Text);
                }
            }
            catch { }

            PopulateTPH(_estateSpinner.SelectedItem.ToString(), _afdelingSpinner.SelectedItem.ToString(), _blockItems[e.Position].BlockCode);
            _idx = 0;
            ShowData(_idx);
            _processProgress.Dismiss();
        }

        void PrevButton_OnClick(object sender, EventArgs e)
        {
            if (FindViewById<EditText>(Resource.Id.et_remarks).Text != _gpsCoordinates[_idx].Remarks)
            {
                SaveData(_idx, FindViewById<EditText>(Resource.Id.et_remarks).Text);
            }

            _idx--;
            ShowData(_idx);
        }

        void NextButton_OnClick(object sender, EventArgs e)
        {
            if (FindViewById<EditText>(Resource.Id.et_remarks).Text != _gpsCoordinates[_idx].Remarks)
            {
                SaveData(_idx, FindViewById<EditText>(Resource.Id.et_remarks).Text);
            }

            _idx++;
            ShowData(_idx);
        }

        void GoButton_OnClick(object sender, EventArgs e)
        {
            if (FindViewById<EditText>(Resource.Id.et_remarks).Text != _gpsCoordinates[_idx].Remarks)
            {
                SaveData(_idx, FindViewById<EditText>(Resource.Id.et_remarks).Text);
            }

            int idx = -1;
            for (int i = 0; i < _gpsCoordinates.Count; i++)
            {
                if (FindViewById<EditText>(Resource.Id.et_tph).Text == _gpsCoordinates[i].TPH)
                    idx = i;
            }

            if (idx != -1)
            {
                _idx = idx;
                ShowData(_idx);
            }
            else
            {
                AlertDialog.Builder alert = new AlertDialog.Builder(this);
                alert.SetTitle("Error");
                alert.SetMessage("TPH is not found");
                alert.SetPositiveButton("OK", (senderAlert, args) =>
                {
                    FindViewById<EditText>(Resource.Id.et_tph).Text = _gpsCoordinates[_idx].TPH;

                    try
                    {
                        InputMethodManager inputManager = (InputMethodManager)this.GetSystemService(Context.InputMethodService);
                        var currentFocus = this.CurrentFocus;
                        if (currentFocus != null)
                        {
                            inputManager.HideSoftInputFromWindow(currentFocus.WindowToken, HideSoftInputFlags.None);
                        }
                    }
                    catch { }
                });

                RunOnUiThread(() =>
                {
                    alert.Show();
                });
            }
        }

        void UploadButton_OnClick(object sender, EventArgs e)
        {
            _uploadProgress = new ProgressDialog(this);
            _uploadProgress.Indeterminate = false;
            _uploadProgress.Max = 100;
            _uploadProgress.SetProgressStyle(ProgressDialogStyle.Horizontal);
            _uploadProgress.SetMessage("Uploading...");
            _uploadProgress.SetCancelable(false);
            _uploadProgress.Show();

            BackgroundWorker uploadWorker = new BackgroundWorker();
            uploadWorker.DoWork += uploadWorker_DoWork;
            uploadWorker.RunWorkerCompleted += uploadWorker_RunWorkerCompleted;
            uploadWorker.RunWorkerAsync();
        }

        void uploadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            List<TphInfo> infos = SqliteDatabase.GetCoordinates();
            if (infos != null && infos.Count > 0)
            {
                _AllData = true;
                foreach (TphInfo info in infos)
                {
                    string requestData = JsonConvert.SerializeObject(info,
                                                      new JsonSerializerSettings() { DateFormatHandling = DateFormatHandling.MicrosoftDateFormat });
                    RestClient client = new RestClient(CacheManager.URL, HttpVerb.POST, ContentTypeString.JSON, requestData);
                    string strResult = client.ProcessRequest("UpdateTph", null);
                    if (string.IsNullOrEmpty(strResult))
                        SqliteDatabase.UpdateSentStatus(info);
                    else
                        _AllData = false;

                    _uploadProgress.IncrementProgressBy(100 / infos.Count);
                }
            }
            else
                _uploadProgress.IncrementProgressBy(100);
        }

        void uploadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _uploadProgress.Dismiss();

            if (_AllData)
            {
                AlertDialog.Builder alert = new AlertDialog.Builder(this);
                alert.SetTitle("Info");
                alert.SetMessage("Data has been uploaded successfully");
                alert.SetPositiveButton("OK", (senderAlert, args) =>
                {
                });

                RunOnUiThread(() =>
                {
                    alert.Show();
                });
            }
            else
            {
                AlertDialog.Builder alert = new AlertDialog.Builder(this);
                alert.SetTitle("Info");
                alert.SetMessage("Failed to update some data. Please try to re-upload.");
                alert.SetPositiveButton("OK", (senderAlert, args) =>
                {
                });

                RunOnUiThread(() =>
                {
                    alert.Show();
                });
            }
        }

        void DownloadButton_OnClick(object sender, EventArgs e)
        {
            _downloadProgress = new ProgressDialog(this);
            _downloadProgress.Indeterminate = true;
            _downloadProgress.Max = 100;
            _downloadProgress.SetProgressStyle(ProgressDialogStyle.Horizontal);
            _downloadProgress.SetMessage("Downloading...");
            _downloadProgress.SetCancelable(false);
            _downloadProgress.Progress = 0;
            _downloadProgress.Show();
            _tmrCounter.Start();

            BackgroundWorker downloadWorker = new BackgroundWorker();
            downloadWorker.DoWork += downloadWorker_DoWork;
            downloadWorker.RunWorkerCompleted += downloadWorker_RunWorkerCompleted;
            downloadWorker.RunWorkerAsync();
        }

        void tmrCounter_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_downloadProgress.Progress < 100)
            {
                if (_downloadProgress.Progress + 2 < 100)
                    _downloadProgress.IncrementProgressBy(2);
                else
                    _downloadProgress.Progress = 100;
            }
        }

        void downloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client = new RestClient(CacheManager.URL, HttpVerb.POST, ContentTypeString.JSON, string.Empty);
            string strResult = client.ProcessRequest("DownloadEstates", null);
            try
            {
                if (!string.IsNullOrEmpty(strResult))
                {
                    List<string> estates = new List<string>();
                    try
                    {
                        estates = JsonConvert.DeserializeObject<List<string>>(strResult);
                    }
                    catch { }

                    SqliteDatabase.ClearTables();
                    List<TPHTable> datas = new List<TPHTable>();
                    foreach(string estate in estates)
                    {
                        client = new RestClient(CacheManager.URL, HttpVerb.POST, ContentTypeString.JSON, "\"" + estate + "\"");
                        string strTphResult = client.ProcessRequest("DownloadTphs", null);

                        try
                        {
                            if (!string.IsNullOrEmpty(strTphResult))
                            {
                                List<TphInfo> tphs = new List<TphInfo>();
                                try
                                {
                                    tphs = JsonConvert.DeserializeObject<List<TphInfo>>(strTphResult);
                                }
                                catch { }

                                datas.AddRange(SqliteDatabase.CombineData(tphs));
                            }
                        }
                        catch { }
                    }
                    SqliteDatabase.InsertTph(datas);
                    SqliteDatabase.Commit();
                    datas = null;
                    Thread.Sleep(1000);
                    List<string> temp = SqliteDatabase.GetEstates();
                }
            }
            catch { }
        }

        void downloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            PopulateEstate();

            _downloadProgress.Progress = 100;
            _tmrCounter.Stop();
            _downloadProgress.Dismiss();
        }

        void ShowData(int idx)
        {
            if (_gpsCoordinates.Count > 0)
            {
                FindViewById<EditText>(Resource.Id.et_tph).Text = _gpsCoordinates[idx].TPH;
                if (_gpsCoordinates[idx].Latitude != 0 && _gpsCoordinates[idx].Longitude != 0)
                {
                    _latitudeText.Text = string.Format("{0:f7}", _gpsCoordinates[idx].Latitude);
                    _longitudeText.Text = string.Format("{0:f7}", _gpsCoordinates[idx].Longitude);
                }
                else
                {
                    _latitudeText.Text = string.Empty;
                    _longitudeText.Text = string.Empty;
                }
                FindViewById<EditText>(Resource.Id.et_remarks).Text = _gpsCoordinates[idx].Remarks;

                FindViewById<EditText>(Resource.Id.et_accuracy).Text = "";
                FindViewById<Button>(Resource.Id.btn_prev).Enabled = idx != 0;
                FindViewById<Button>(Resource.Id.btn_next).Enabled = idx != (_gpsCoordinates.Count - 1);
                FindViewById<Button>(Resource.Id.btn_go).Enabled = false;
                FindViewById<Button>(Resource.Id.btn_get_coordinates).Enabled = true;
            }

            try
            {
                InputMethodManager inputManager = (InputMethodManager)this.GetSystemService(Context.InputMethodService);
                var currentFocus = this.CurrentFocus;
                if (currentFocus != null)
                {
                    inputManager.HideSoftInputFromWindow(currentFocus.WindowToken, HideSoftInputFlags.None);
                }
            }
            catch { }
        }

        bool SaveData(int idx, double latitude, double longitude, string remarks)
        {
            try
            {
                SqliteDatabase.UpdateTph(new TPHTable()
                {
                    Estate = _gpsCoordinates[_idx].Estate,
                    Afdeling = _gpsCoordinates[_idx].Afdeling,
                    Block = _gpsCoordinates[_idx].Block,
                    TPH = _gpsCoordinates[_idx].TPH,
                    Latitude = Convert.ToDecimal(latitude),
                    Longitude = Convert.ToDecimal(longitude),
                    Remarks = FindViewById<EditText>(Resource.Id.et_remarks).Text
                });

                _gpsCoordinates[_idx].Latitude = Convert.ToDecimal(latitude);
                _gpsCoordinates[_idx].Longitude = Convert.ToDecimal(longitude);
                _gpsCoordinates[_idx].Remarks = FindViewById<EditText>(Resource.Id.et_remarks).Text;

                return true;
            }
            catch
            {
                return false;
            }
        }

        bool SaveData(int idx, string remarks)
        {
            try
            {
                SqliteDatabase.UpdateTph(new TPHTable()
                {
                    Estate = _gpsCoordinates[_idx].Estate,
                    Afdeling = _gpsCoordinates[_idx].Afdeling,
                    Block = _gpsCoordinates[_idx].Block,
                    TPH = _gpsCoordinates[_idx].TPH,
                    Remarks = FindViewById<EditText>(Resource.Id.et_remarks).Text
                });

                _gpsCoordinates[_idx].Remarks = FindViewById<EditText>(Resource.Id.et_remarks).Text;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /* Checks if external storage is available for read and write */
        public bool IsExternalStorageWritable()
        {
            if (Android.OS.Environment.ExternalStorageState == Android.OS.Environment.MediaMounted)
            {
                return true;
            }
            return false;
        }

        void GetCoordinatesButton_OnClick(object sender, EventArgs eventArgs)
        {
            _latitudeText.Text = string.Format("");
            _longitudeText.Text = string.Format("");
            _accuracyText.Text = string.Empty;
            _getAddress = true;
            _locationProgress.Show();

            try
            {
                _locationManager.RequestLocationUpdates(_locationProvider, 1000, 1, this);
            }
            catch { }
        }

        void CancelEvent()
        {
            _locationProgress.Dismiss();

            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetTitle("Error");
            alert.SetMessage("Can't determine the current location. Try again in a few seconds.");
            alert.SetPositiveButton("OK", (senderAlert, args) =>
            {
            });

            RunOnUiThread(() =>
            {
                alert.Show();
            });

            try
            {
                _locationManager.RemoveUpdates(this);
            }
            catch { }
        }

        void PopulateEstate()
        {
            List<string> estates = SqliteDatabase.GetEstates();
            if (estates != null && estates.Count > 0)
            {
                _estateItems = new EstateAdapter(this, estates.ToArray());
                FindViewById<Spinner>(Resource.Id.spin_estate).Adapter = _estateItems;

                FindViewById<Spinner>(Resource.Id.spin_estate).Enabled = true;
                FindViewById<Spinner>(Resource.Id.spin_afdeling).Enabled = true;
                FindViewById<Spinner>(Resource.Id.spin_block).Enabled = true;
                FindViewById<Button>(Resource.Id.btn_get_coordinates).Enabled = true;
                FindViewById<Button>(Resource.Id.btn_go).Enabled = true;
                FindViewById<Button>(Resource.Id.btn_next).Enabled = true;
                FindViewById<Button>(Resource.Id.btn_prev).Enabled = true;
                FindViewById<EditText>(Resource.Id.et_remarks).Enabled = true;
                FindViewById<EditText>(Resource.Id.et_tph).Enabled = true;
            }
            else
            {
                FindViewById<Spinner>(Resource.Id.spin_estate).Adapter = null;

                FindViewById<Spinner>(Resource.Id.spin_estate).Enabled = false;
                FindViewById<Spinner>(Resource.Id.spin_afdeling).Enabled = false;
                FindViewById<Spinner>(Resource.Id.spin_block).Enabled = false;
                FindViewById<Button>(Resource.Id.btn_get_coordinates).Enabled = false;
                FindViewById<Button>(Resource.Id.btn_go).Enabled = false;
                FindViewById<Button>(Resource.Id.btn_next).Enabled = false;
                FindViewById<Button>(Resource.Id.btn_prev).Enabled = false;
                FindViewById<EditText>(Resource.Id.et_remarks).Enabled = false;
                FindViewById<EditText>(Resource.Id.et_tph).Enabled = false;
            }
        }

        void PopulateAfdeling(string estateCode)
        {
            List<string> afdelings = SqliteDatabase.GetAfdelings(estateCode);
            if (afdelings != null)
            {
                _afdelingItems = new AfdelingAdapter(this, afdelings.ToArray());
                FindViewById<Spinner>(Resource.Id.spin_afdeling).Adapter = _afdelingItems;
            }
            else
            {
                FindViewById<Spinner>(Resource.Id.spin_afdeling).Adapter = null;
            }
        }

        void PopulateBlock(string estateCode, string afdelingCode)
        {
            List<Block> blocks = SqliteDatabase.GetBlocks(estateCode, afdelingCode);
            if (blocks != null)
            {
                _blockItems = new BlockAdapter(this, blocks.ToArray());
                FindViewById<Spinner>(Resource.Id.spin_block).Adapter = _blockItems;
            }
            else
            {
                FindViewById<Spinner>(Resource.Id.spin_block).Adapter = null;
            }
        }

        void PopulateTPH(string estateCode, string afdelingCode, string blockCode)
        {
            _gpsCoordinates = SqliteDatabase.GetTphs(estateCode, afdelingCode, blockCode);
        }

        void InitializeLocationManager()
        {
            _locationManager = (LocationManager)GetSystemService(LocationService);
            Criteria criteriaForLocationService = new Criteria
            {
                Accuracy = Accuracy.Fine
            };
            IList<string> acceptableLocationProviders = _locationManager.GetProviders(criteriaForLocationService, true);

            if (acceptableLocationProviders.Any())
            {
                if (acceptableLocationProviders.Count > 1)
                    _locationProvider = acceptableLocationProviders[1];
                else
                    _locationProvider = acceptableLocationProviders.First();
            }
            else
            {
                _locationProvider = string.Empty;
            }

            if (!_locationManager.IsProviderEnabled(_locationProvider))
            {
                AlertDialog.Builder alert = new AlertDialog.Builder(this);
                alert.SetTitle("Error");
                alert.SetMessage("Please enable GPS location");
                alert.SetPositiveButton("OK", (senderAlert, args) =>
                {
                    Finish();
                });

                RunOnUiThread(() =>
                {
                    alert.Show();
                });
            }
        }

        public override void OnBackPressed()
        {
            if (_DoubleBackToExitPressedOnce)
            {
                base.OnBackPressed();
                return;
            }

            this._DoubleBackToExitPressedOnce = true;
            Toast.MakeText(this, "Please click BACK again to exit", ToastLength.Short).Show();

            new Handler().PostDelayed(new Action(() => { _DoubleBackToExitPressedOnce = false; }), 2000);
        }

        protected override void OnResume()
        {
            base.OnResume();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            try
            {
                _locationManager.RemoveUpdates(this);
            }
            catch { }
        }

        public async void OnLocationChanged(Location location)
        {
            if (location.Accuracy < 50 && _getAddress)
            {
                _locationProgress.Dismiss();
                _latitudeText.Text = string.Format("{0:f7}", location.Latitude);
                _longitudeText.Text = string.Format("{0:f7}", location.Longitude);
                _getAddress = false;

                _accuracyText.Text = string.Format("{0:f2}", location.Accuracy);

                if (SaveData(_idx, location.Latitude, location.Longitude,
                    FindViewById<EditText>(Resource.Id.et_remarks).Text))
                {
                    //set alert for executing the task
                    AlertDialog.Builder alert = new AlertDialog.Builder(this);
                    alert.SetTitle("Information");
                    alert.SetMessage("GPS coordinate is received and saved successfully.");
                    alert.SetPositiveButton("OK", (senderAlert, args) =>
                    {
                        //Action here
                    });

                    RunOnUiThread(() =>
                    {
                        alert.Show();
                    });
                }
                else
                {
                    AlertDialog.Builder alert = new AlertDialog.Builder(this);
                    alert.SetTitle("Error");
                    alert.SetMessage("GPS coordinate is not saved properly. Please retry.");
                    alert.SetPositiveButton("OK", (senderAlert, args) =>
                    {

                    });

                    RunOnUiThread(() =>
                    {
                        alert.Show();
                    });
                }

                try
                {
                    _locationManager.RemoveUpdates(this);
                }
                catch { }
            }
        }

        public void OnProviderDisabled(string provider) { }

        public void OnProviderEnabled(string provider) { }

        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }
    }
}

