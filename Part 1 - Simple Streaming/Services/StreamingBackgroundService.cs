using System.Runtime.InteropServices;
using Android.App;
using System;
using Android.Content;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;

namespace BackgroundStreamingAudio.Services
{
    [Service]
    [IntentFilter(new[] { ActionPlay, ActionPause, ActionStop })]
    public class StreamingBackgroundService : Service
    {
        //Commands
        public const string ActionPlay = "com.xamarin.action.PLAY";
        public const string ActionPause = "com.xamarin.action.PAUSE";
        public const string ActionStop = "com.xamarin.action.STOP";

        private string mp3 = @"http://www.montemagno.com/sample.mp3";

        private MediaPlayer player;
        //private AudioManager audioManager;
       // private NotificationManager notificationManager;
        private WifiManager.WifiLock wifiLock;
        private bool paused = false;

        private int notificationId = 1;

        /// <summary>
        /// On create simply detect some of our managers
        /// </summary>
        public override void OnCreate()
        {
            base.OnCreate();
            //Find our audio and notificaton managers
           // audioManager = (AudioManager)GetSystemService(AudioService);
            //notificationManager = (NotificationManager) GetSystemService(NotificationService);

        }

        /// <summary>
        /// Don't do anything on bind
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {

            switch (intent.Action)
            {
                case ActionPlay:
                    Play();
                    break;
                case ActionStop:
                    Stop();
                    break;
                case ActionPause:
                    Pause();
                    break;
            }

            //Set sticky as we are a long running operation
            return StartCommandResult.Sticky;
        }

        private async void Play()
        {

            if (paused && player != null) {
                paused = false;
                //We are simply paused so just start again
                player.Start();
                StartForeground();
                return;
            }

            if (player == null) {
                
                player = new MediaPlayer();

                //Tell our player to sream music
                player.SetAudioStreamType(Stream.Music);

                //Wake mode will be partial to keep the CPU still running under lock screen
                player.SetWakeMode(ApplicationContext, WakeLockFlags.Partial);

                //When we have prepared the song start playback
                player.Prepared += (sender, args) => player.Start();

                //When we have reached the end of the song stop ourselves, however you could signal next track here.
                player.Completion += (sender, args) => Stop();

                player.Error += (sender, args) => {
                    //playback error
                    Console.WriteLine("Error in playback resetting: " + args.What);
                    Stop();//this will clean up and reset properly.
                };
            }

            if (player.IsPlaying)
                return;

            try {
                await player.SetDataSourceAsync(ApplicationContext, Android.Net.Uri.Parse(mp3));
                player.PrepareAsync();
                AquireWifiLock();
                StartForeground();
            }
            catch (Exception ex) {
                //unable to start playback log error
                Console.WriteLine("Unable to start playback: " + ex);
            }
        }

        /// <summary>
        /// When we start on the foreground we will present a notification to the user
        /// When they press the notification it will take them to the main page so they can control the music
        /// </summary>
        private void StartForeground()
        {
            
            var pendingIntent = PendingIntent.GetActivity(ApplicationContext, 0,
                            new Intent(ApplicationContext, typeof(MainActivity)),
                            PendingIntentFlags.UpdateCurrent);

            var notification = new Notification
            {
                TickerText = new Java.Lang.String("Song started!"),
                Icon = Resource.Drawable.ic_stat_av_play_over_video
            };
            notification.Flags |= NotificationFlags.OngoingEvent;
            notification.SetLatestEventInfo(ApplicationContext, "Xamarin Streaming",
                            "Playing music!", pendingIntent);
            StartForeground(notificationId, notification);
        }

        private void Pause()
        {
            if (player == null)
                return;

            if(player.IsPlaying)
                player.Pause();

            StopForeground(true);
            paused = true;
        }

        private void Stop()
        {
            if (player == null)
                return;

            if(player.IsPlaying)
                player.Stop();

            player.Reset();
            paused = false;
            StopForeground(true);
            ReleaseWifiLock();
        }

        /// <summary>
        /// Lock the wifi so we can still stream under lock screen
        /// </summary>
        private void AquireWifiLock()
        {
            if (wifiLock == null){
                var wifiService = (WifiManager) GetSystemService(WifiService);
                wifiLock = wifiService.CreateWifiLock(WifiMode.Full, "xamarin_wifi_lock");
            } 
            wifiLock.Acquire();
        }

        /// <summary>
        /// This will release the wifi lock if it is no longer needed
        /// </summary>
        private void ReleaseWifiLock()
        {
            if (wifiLock == null)
                return;

            wifiLock.Release();
            wifiLock = null;
        }

        /// <summary>
        /// Properly cleanup of your player by releasing resources
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (player != null)
            {
                player.Release();
                player = null;
            }
        }
    }
}
