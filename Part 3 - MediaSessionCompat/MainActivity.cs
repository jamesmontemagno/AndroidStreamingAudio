using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.Media.Session;
using Android.Support.V7.App;
using Android.Widget;
using BackgroundStreamingAudio.Services;
using Toolbar = Android.Support.V7.Widget.Toolbar;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;

namespace BackgroundStreamingAudio
{
    [Activity(Label = "BackgroundStreamingAudio", 
        MainLauncher = true,
        Theme = "@style/AppTheme",
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize
    )]
    public class MainActivity : AppCompatActivity
    {
        bool isBound = false;
        private MediaPlayerServiceBinder binder;
        MediaPlayerServiceConnection mediaPlayerServiceConnection;
        private Intent mediaPlayerServiceIntent;

        public event StatusChangedEventHandler StatusChanged;

        public event CoverReloadedEventHandler CoverReloaded;

        public event PlayingEventHandler Playing;

        public event BufferingEventHandler Buffering;

        public Toolbar Toolbar {
            get;
            set;
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            Toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            if (Toolbar != null) {
                SetSupportActionBar(Toolbar);
                SupportActionBar.SetDisplayHomeAsUpEnabled(true);
                SupportActionBar.SetHomeButtonEnabled (true);
            }

            if(mediaPlayerServiceConnection == null)
                InitilizeMedia();

            var previous = FindViewById<ImageButton>(Resource.Id.btnPrevious);
            previous.Click += async (sender, args) => {
                if(binder.GetMediaPlayerService().mediaPlayer != null)
                    await binder.GetMediaPlayerService().PlayPrevious();
            };

            var playpause = FindViewById<Button>(Resource.Id.btnPlayPause);
            playpause.Click += async (sender, args) => {
                if(binder.GetMediaPlayerService().mediaPlayer != null && binder.GetMediaPlayerService().MediaPlayerState == PlaybackStateCompat.StatePlaying)
                    await binder.GetMediaPlayerService().Pause();
                else
                    await binder.GetMediaPlayerService().Play();
            };

            var next = FindViewById<ImageButton>(Resource.Id.btnNext);
            next.Click += async (sender, args) => {
                if(binder.GetMediaPlayerService().mediaPlayer != null)
                    await binder.GetMediaPlayerService().PlayNext();
            };

            var position = FindViewById<TextView>(Resource.Id.textview_position);
            var duration = FindViewById<TextView>(Resource.Id.textview_duration);
            var seekbar = FindViewById<SeekBar>(Resource.Id.player_seekbar);
            Playing += (object sender, EventArgs e) => {
                seekbar.Max = binder.GetMediaPlayerService().Duration;
                seekbar.Progress = binder.GetMediaPlayerService().Position;

                position.Text = GetFormattedTime(binder.GetMediaPlayerService().Position);
                duration.Text = GetFormattedTime(binder.GetMediaPlayerService().Duration);
            };

            Buffering += (object sender, EventArgs e) => {
                seekbar.SecondaryProgress = binder.GetMediaPlayerService().Buffered;
            };

            CoverReloaded += (object sender, EventArgs e) => {
                var cover = FindViewById<ImageView>(Resource.Id.imageview_cover);
                cover.SetImageBitmap(binder.GetMediaPlayerService().Cover as Bitmap);
            };

            var title = FindViewById<TextView>(Resource.Id.textview_title);
            var subtitle = FindViewById<TextView>(Resource.Id.textview_subtitle);
            StatusChanged += (object sender, EventArgs e) => {
                var metadata = binder.GetMediaPlayerService().mediaControllerCompat.Metadata;
                if(metadata != null)
                {
                    title.Text = metadata.GetString(MediaMetadata.MetadataKeyTitle);
                    subtitle.Text = metadata.GetString(MediaMetadata.MetadataKeyArtist);
                }
            };
        }

        private string GetFormattedTime(int value)
        {
            var span = TimeSpan.FromMilliseconds(value);
            if (span.Hours > 0)
            {
                return string.Format("{0}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
            }
            else
            {
                return string.Format("{0}:{1:00}", (int)span.Minutes, span.Seconds);
            }
        }

        private void InitilizeMedia()
        {
            mediaPlayerServiceIntent = new Intent(ApplicationContext, typeof(MediaPlayerService));
            mediaPlayerServiceConnection = new MediaPlayerServiceConnection (this);
            BindService (mediaPlayerServiceIntent, mediaPlayerServiceConnection, Bind.AutoCreate);
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

