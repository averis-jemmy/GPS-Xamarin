using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Support.V7.App;
using Android.Views.InputMethods;
using Java.Lang.Reflect;
using Android.Animation;
using Android.Graphics;
using System.ComponentModel;
using MyAverisClient;
using Newtonsoft.Json;
using MyAverisEntity;
using Android.Text.Method;
using Android.Text;
using Android.Gms.Maps.Model;
using Android.Gms.Maps;

namespace MyAveris.Droid
{
    [Activity(Label = "AverisLocationActivity", WindowSoftInputMode = SoftInput.StateHidden, Theme = "@style/MyTheme.Base", ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class AverisLocationActivity : AppCompatActivity
    {
        private GoogleMap _map;
        private MapFragment _mapFragment;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.AverisLocation);

            try
            {
                if (CacheManager.mContext == null)
                {
                    Database.newInstance(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "myaveris.db3"));
                    Database.CreateTables();

                    CacheManager.Init(Application.Context);

                    var user = CacheManager.GetFromSharedPreferences();

                    CacheManager.TokenID = user.TokenID;
                    CacheManager.UserID = user.UserID;
                    CacheManager.IsRecruiter = user.IsRecruiter;
                    CacheManager.HasProfilePicture = user.HasProfilePicture;
                    CacheManager.PositionApplied = user.PositionApplied;
                    CacheManager.LoadData();
                }
            }
            catch { }

            // Initialize toolbar
            var toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.AppBar);
            SetSupportActionBar(toolbar);
            SupportActionBar.SetTitle(Resource.String.OfficeLocationTitle);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetDisplayShowHomeEnabled(true);

            InitMapFragment();

            FindViewById<ImageView>(Resource.Id.imgWaze).Click += Waze_Click;
            FindViewById<ImageView>(Resource.Id.imgGoogleMap).Click += Map_Click;

            CacheManager.ProcessProgress.Dismiss();
        }

        void Waze_Click(object sender, EventArgs e)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse("http://waze.to/?ll=3.1119487,101.6671252&navigate=yes"));
            StartActivity(intent);
        }

        void Map_Click(object sender, EventArgs e)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse("http://maps.google.com/maps?daddr=4.10982,101.6671252"));
            StartActivity(intent);
        }

        protected override void OnResume()
        {
            base.OnResume();
            SetupMapIfNeeded();
        }

        private void InitMapFragment()
        {
            _mapFragment = FragmentManager.FindFragmentByTag("map") as MapFragment;
            if (_mapFragment == null)
            {
                GoogleMapOptions mapOptions = new GoogleMapOptions()
                    .InvokeMapType(GoogleMap.MapTypeNormal)
                    .InvokeZoomControlsEnabled(false)
                    .InvokeCompassEnabled(true);

                FragmentTransaction fragTx = FragmentManager.BeginTransaction();
                _mapFragment = MapFragment.NewInstance(mapOptions);
                fragTx.Add(Resource.Id.map, _mapFragment, "map");
                fragTx.Commit();
            }
        }

        private void SetupMapIfNeeded()
        {
            if (_map == null)
            {
                _map = _mapFragment.Map;
                if (_map != null)
                {
                    MarkerOptions markerOpt1 = new MarkerOptions();
                    markerOpt1.SetPosition(CacheManager.AverisCoordinate);
                    markerOpt1.SetTitle("Averis Sdn Bhd");
                    markerOpt1.InvokeIcon(BitmapDescriptorFactory.DefaultMarker(BitmapDescriptorFactory.HueCyan));
                    _map.AddMarker(markerOpt1);
                    _map.MarkerClick += MarkerClick;

                    // We create an instance of CameraUpdate, and move the map to it.
                    CameraUpdate cameraUpdate = CameraUpdateFactory.NewLatLngZoom(CacheManager.AverisCoordinate, 16);
                    _map.MoveCamera(cameraUpdate);
                }
            }
        }

        void MarkerClick(object sender, GoogleMap.MarkerClickEventArgs e)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse("http://waze.to/?ll=4.10982,101.6671252&navigate=yes"));
            StartActivity(intent);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Android.Resource.Id.Home:
                    Finish();
                    return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        protected override void OnPause()
        {
            GC.Collect();
            base.OnPause();
        }

        protected override void OnDestroy()
        {
            GC.Collect();
            base.OnDestroy();
        }

        public override void OnLowMemory()
        {
            GC.Collect();
            base.OnLowMemory();
        }
    }
}