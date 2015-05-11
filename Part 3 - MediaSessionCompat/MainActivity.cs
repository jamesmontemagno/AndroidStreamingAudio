using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using BackgroundStreamingAudio.Services;
using System;

namespace BackgroundStreamingAudio
{
    [Activity(Label = "BackgroundStreamingAudio", MainLauncher = true, Icon = "@drawable/ic_launcher", Theme = "@style/Theme")]
    public class MainActivity : Activity
    {
        bool isBound = false;
        private MediaPlayerServiceBinder binder;
        MediaPlayerServiceConnection mediaPlayerServiceConnection;
        private Intent mediaPlayerServiceIntent;

        public event StatusChangedEventHandler StatusChanged;

        public event CoverReloadedEventHandler CoverReloaded;

        public event PlayingEventHandler Playing;

        public event BufferingEventHandler Buffering;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            var play = FindViewById<Button>(Resource.Id.playButton);
            var pause = FindViewById<Button>(Resource.Id.pauseButton);
            var stop = FindViewById<Button>(Resource.Id.stopButton);

            if(mediaPlayerServiceConnection == null)
                InitilizeMedia();

            play.Click += async (sender, args) => {
                await binder.GetMediaPlayerService().Play();
            };
            //pause.Click += (sender, args) => SendAudioCommand(StreamingBackgroundService.ActionPause);
            //stop.Click += (sender, args) => SendAudioCommand(StreamingBackgroundService.ActionStop);

			var musicBy = FindViewById<TextView> (Resource.Id.musicbytext);
			musicBy.Clickable = true;
			musicBy.Click += (sender, e) => {
                

				/*var musicByIntent = new Intent(Intent.ActionView);
				musicByIntent.SetData(Android.Net.Uri.Parse("http://freemusicarchive.org/music/Raw_Stiles/STOP_2X2_04/A2_Raw_Stiles_-_Rouge_1291"));
				StartActivity(musicByIntent);*/
			};

        }

        private void InitilizeMedia()
        {
            mediaPlayerServiceIntent = new Intent(ApplicationContext, typeof(MediaPlayerService));
            mediaPlayerServiceConnection = new MediaPlayerServiceConnection (this);
            BindService (mediaPlayerServiceIntent, mediaPlayerServiceConnection, Bind.AutoCreate);
        }

        private void SendAudioCommand(string action)
        {
            var intent = new Intent(action);
            StartService(intent);
        }

        class MediaPlayerServiceConnection : Java.Lang.Object, IServiceConnection
        {
            MainActivity instance;

            public MediaPlayerServiceConnection (MainActivity mediaPlayer)
            {
                this.instance = mediaPlayer;
            }

            public void OnServiceConnected (ComponentName name, IBinder service)
            {
                var mediaPlayerServiceBinder = service as MediaPlayerServiceBinder;
                if (mediaPlayerServiceBinder != null) {
                    var binder = (MediaPlayerServiceBinder)service;
                    instance.binder = binder;
                    instance.isBound = true;

                    binder.GetMediaPlayerService().CoverReloaded += (object sender, EventArgs e) => { if (instance.CoverReloaded != null) instance.CoverReloaded(sender, e); };
                    binder.GetMediaPlayerService().StatusChanged += (object sender, EventArgs e) => { if (instance.StatusChanged != null) instance.StatusChanged(sender, e); };
                    binder.GetMediaPlayerService().Playing += (object sender, EventArgs e) => { if (instance.Playing != null) instance.Playing(sender, e); };
                    binder.GetMediaPlayerService().Buffering += (object sender, EventArgs e) => { if (instance.Buffering != null) instance.Buffering(sender, e); };
                }
            }

            public void OnServiceDisconnected (ComponentName name)
            {
                instance.isBound = false;
            }
        }
    }
}

