using Android.App;
using System;
using Android.Content;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Graphics;
using Android.Support.V4.Media.Session;
using Android.Support.V4.Media;
using Android.Support.V4.App;
using System.Threading.Tasks;

namespace BackgroundStreamingAudio.Services
{
    [Service]
    [IntentFilter (new[] { ActionPlay, ActionPause, ActionStop, ActionTogglePlayback, ActionNext, ActionPrevious })]
    public class MediaPlayerService : Service, AudioManager.IOnAudioFocusChangeListener, 
    MediaPlayer.IOnBufferingUpdateListener, 
    MediaPlayer.IOnCompletionListener, 
    MediaPlayer.IOnErrorListener, 
    MediaPlayer.IOnPreparedListener, 
    MediaPlayer.IOnSeekCompleteListener
    {
        //Actions
        public const string ActionPlay = "com.xamarin.action.PLAY";
        public const string ActionPause = "com.xamarin.action.PAUSE";
        public const string ActionStop = "com.xamarin.action.STOP";
		public const string ActionTogglePlayback = "com.xamarin.action.TOGGLEPLAYBACK";
		public const string ActionNext = "com.xamarin.action.NEXT";
		public const string ActionPrevious = "com.xamarin.action.PREVIOUS";

        private const string audioUrl = @"http://www.montemagno.com/sample.mp3";

        public MediaPlayer mediaPlayer;
        private AudioManager audioManager;

        private MediaSessionCompat mediaSessionCompat;
        public MediaControllerCompat mediaControllerCompat;

        public int MediaPlayerState
        {
            get{
                return mediaControllerCompat.PlaybackState.State;
            }
        }

        private WifiManager wifiManager;
        private WifiManager.WifiLock wifiLock;
        private ComponentName remoteComponentName;

        private const int NotificationId = 1;

        public event StatusChangedEventHandler StatusChanged;

        public event CoverReloadedEventHandler CoverReloaded;

        public event PlayingEventHandler Playing;

        public event BufferingEventHandler Buffering;

        private Handler PlayingHandler;
        private Java.Lang.Runnable PlayingHandlerRunnable;

        public MediaPlayerService ()
        {
            // Create an instance for a runnable-handler
            PlayingHandler = new Handler ();

            // Create a runnable, restarting itself if the status still is "playing"
            PlayingHandlerRunnable = new Java.Lang.Runnable (() => {
                OnPlaying (EventArgs.Empty);

                if (MediaPlayerState == PlaybackStateCompat.StatePlaying) {
                    PlayingHandler.PostDelayed (PlayingHandlerRunnable, 250);
                }
            });

            // On Status changed to PLAYING, start raising the Playing event
            StatusChanged += (object sender, EventArgs e) => {
                if(MediaPlayerState == PlaybackStateCompat.StatePlaying){
                    PlayingHandler.PostDelayed (PlayingHandlerRunnable, 0);
                }
            };
        }

        protected virtual void OnStatusChanged (EventArgs e)
        {
            if (StatusChanged != null)
                StatusChanged (this, e);
        }

        protected virtual void OnCoverReloaded (EventArgs e)
        {
            if (CoverReloaded != null) {
                CoverReloaded (this, e);
                StartNotification ();
                UpdateMediaMetadataCompat ();
            }
        }

        protected virtual void OnPlaying (EventArgs e)
        {
            if (Playing != null)
                Playing (this, e);
        }

        protected virtual void OnBuffering (EventArgs e)
        {
            if (Buffering != null)
                Buffering (this, e);
        }

        /// <summary>
        /// On create simply detect some of our managers
        /// </summary>
        public override void OnCreate()
        {
            base.OnCreate ();
            //Find our audio and notificaton managers
            audioManager = (AudioManager)GetSystemService (AudioService);
            wifiManager = (WifiManager)GetSystemService (WifiService);

            remoteComponentName = new ComponentName (PackageName, new RemoteControlBroadcastReceiver ().ComponentName);
        }

        /// <summary>
        /// Will register for the remote control client commands in audio manager
        /// </summary>
        private void InitMediaSession()
        {
            try {
                if (mediaSessionCompat == null) {
                    Intent nIntent = new Intent(ApplicationContext, typeof(MainActivity));
                    PendingIntent pIntent = PendingIntent.GetActivity(ApplicationContext, 0, nIntent, 0);

                    remoteComponentName = new ComponentName (PackageName, new RemoteControlBroadcastReceiver ().ComponentName);

                    mediaSessionCompat = new MediaSessionCompat (ApplicationContext, "XamarinStreamingAudio", remoteComponentName, pIntent);
                    mediaControllerCompat = new MediaControllerCompat(ApplicationContext, mediaSessionCompat.SessionToken);
                }

                mediaSessionCompat.Active = true;
                mediaSessionCompat.SetCallback (new MediaSessionCallback());

                mediaSessionCompat.SetFlags(MediaSessionCompat.FlagHandlesMediaButtons | MediaSessionCompat.FlagHandlesTransportControls);
            } 
            catch (Exception ex) {
                Console.WriteLine (ex);
            }
        }

        /// <summary>
        /// Intializes the player.
        /// </summary>
        private void InitializePlayer ()
        {
            mediaPlayer = new MediaPlayer ();

            //Tell our player to sream music
            mediaPlayer.SetAudioStreamType (Stream.Music);

            //Wake mode will be partial to keep the CPU still running under lock screen
            mediaPlayer.SetWakeMode (ApplicationContext, WakeLockFlags.Partial);

            mediaPlayer.SetOnBufferingUpdateListener (this);
            mediaPlayer.SetOnCompletionListener (this);
            mediaPlayer.SetOnErrorListener (this);
            mediaPlayer.SetOnPreparedListener (this);
        }


        public void OnBufferingUpdate (MediaPlayer mp, int percent)
        {
            int duration = 0;
            if (MediaPlayerState == PlaybackStateCompat.StatePlaying || MediaPlayerState == PlaybackStateCompat.StatePaused)
                duration = mp.Duration;

            int newBufferedTime = duration * percent / 100;
            if (newBufferedTime != Buffered) {
                Buffered = newBufferedTime;
            }
        }

        public async void OnCompletion (MediaPlayer mp)
        {
            await PlayNext ();
        }

        public bool OnError (MediaPlayer mp, MediaError what, int extra)
        {
            Stop ();
            UpdatePlaybackState(PlaybackStateCompat.StateError);

            return true;
        }

        public void OnSeekComplete (MediaPlayer mp)
        {
            //TODO: Implement buffering on seeking
        }

        public void OnPrepared (MediaPlayer mp)
        {
            //Mediaplayer is prepared start track playback
            mp.Start ();
            UpdatePlaybackState(PlaybackStateCompat.StatePlaying);
        }

        public int Position {
            get {
                if (mediaPlayer == null 
                    || (MediaPlayerState != PlaybackStateCompat.StatePlaying 
                        && MediaPlayerState != PlaybackStateCompat.StatePaused))
                    return -1;
                else
                    return mediaPlayer.CurrentPosition;
            }
        }

        public int Duration {
            get {
                if (mediaPlayer == null 
                    || (MediaPlayerState != PlaybackStateCompat.StatePlaying 
                        && MediaPlayerState != PlaybackStateCompat.StatePaused))
                    return 0;
                else
                    return mediaPlayer.Duration;
            }
        }

        private int buffered = 0;

        public int Buffered {
            get {
                if (mediaPlayer == null)
                    return 0;
                else
                    return buffered;
            }
            private set {
                buffered = value;
                OnBuffering (EventArgs.Empty);
            }
        }

        private Bitmap cover;

        public object Cover {
            get {
                if(cover == null)
                    cover = BitmapFactory.DecodeResource(Resources, Resource.Drawable.album_art);
                return cover;
            }
            private set {
                cover = value as Bitmap;
                OnCoverReloaded (EventArgs.Empty);
            }
        }

		/// <summary>
		/// Intializes the player.
		/// </summary>
        public async Task Play ()
        {
            if (mediaPlayer != null && MediaPlayerState == PlaybackStateCompat.StatePaused) {
                //We are simply paused so just start again
                mediaPlayer.Start ();
                UpdatePlaybackState(PlaybackStateCompat.StatePlaying);
                StartNotification ();

                //Update the metadata now that we are playing
                UpdateMediaMetadataCompat ();
                return;
            }

            if (mediaPlayer == null)
                InitializePlayer ();

            if(mediaSessionCompat == null)
                InitMediaSession ();

            if (mediaPlayer.IsPlaying) {
                UpdatePlaybackState(PlaybackStateCompat.StatePlaying);
                return;
            }

            try {
                MediaMetadataRetriever metaRetriever = new MediaMetadataRetriever ();

                await mediaPlayer.SetDataSourceAsync (ApplicationContext, Android.Net.Uri.Parse (audioUrl));

                //TODO: Find out why this crashes
                //await metaRetriever.SetDataSourceAsync(ApplicationContext, Android.Net.Uri.Parse (audioUrl));

                var focusResult = audioManager.RequestAudioFocus (this, Stream.Music, AudioFocus.Gain);
                if (focusResult != AudioFocusRequest.Granted) {
                    //could not get audio focus
                    Console.WriteLine("Could not get audio focus");
                }

                UpdatePlaybackState(PlaybackStateCompat.StateBuffering);
                mediaPlayer.PrepareAsync ();

                AquireWifiLock ();
                UpdateMediaMetadataCompat (metaRetriever);
                StartNotification ();

                byte[] imageByteArray = metaRetriever.GetEmbeddedPicture ();
                if (imageByteArray == null)
                    Cover = await BitmapFactory.DecodeResourceAsync(Resources, Resource.Drawable.album_art);
                else
                    Cover = await BitmapFactory.DecodeByteArrayAsync (imageByteArray, 0, imageByteArray.Length);
            } catch (Exception ex) {
                UpdatePlaybackState(PlaybackStateCompat.StateStopped);

                //unable to start playback log error
                Console.WriteLine(ex);
            }
        }

        public async Task Seek (int position)
        {
            await Task.Run (() => {
                if (mediaPlayer != null)
                {    
                    mediaPlayer.SeekTo (position);
                }
            });
        }

        public async Task PlayNext ()
        {
            /*if (Queue.HasNext ()) {
                UpdatePlaybackState(PlaybackStateCompat.StateStopped);
                mediaPlayer.Reset ();

                Queue.SetNextAsCurrent ();

                await Play ();
            } else {
                // If you don't have a next song in the queue, stop and show the meta-data of the first song.
                UpdatePlaybackState(PlaybackStateCompat.StateStopped);
                mediaPlayer.Reset ();

                Queue.SetIndexAsCurrent (0);
            }*/
        }

        public async Task PlayPrevious ()
        {
            // Start current track from beginning if it's the first track or the track has played more than 3sec and you hit "playPrevious".
            /*if (!Queue.HasPrevious () || Position > 3000) {
                await Seek (0);
            } else {
                UpdatePlaybackState(PlaybackStateCompat.StateStopped);
                mediaPlayer.Reset ();

                Queue.SetPreviousAsCurrent ();

                await Play ();
            }*/
        }

        public async Task PlayPause ()
        {
            if (MediaPlayerState == PlaybackStateCompat.StatePaused) {
                await Play ();
            } else {
                await Pause ();
            }
        }

        public async Task Pause ()
        {
            await Task.Run (() => {
                if (mediaPlayer == null)
                    return;

                if (mediaPlayer.IsPlaying)
                    mediaPlayer.Pause ();

                UpdatePlaybackState(PlaybackStateCompat.StatePaused);
            });
        }

        public async Task Stop ()
        {
            await Task.Run (() => {
                if (mediaPlayer == null)
                    return;

                if (mediaPlayer.IsPlaying) {
                    mediaPlayer.Stop ();
                }

                UpdatePlaybackState(PlaybackStateCompat.StateStopped);
                mediaPlayer.Reset ();
                StopNotification();
                StopForeground (true);
                ReleaseWifiLock ();
                UnregisterMediaSessionCompat ();
            });
        }

        private void UpdatePlaybackState(int state) {

            if (mediaSessionCompat == null || mediaPlayer == null)
                return;

            try
            {
                PlaybackStateCompat.Builder stateBuilder = new PlaybackStateCompat.Builder()
                    .SetActions(PlaybackStateCompat.ActionPlay 
                        | PlaybackStateCompat.ActionPlayPause
                        | PlaybackStateCompat.ActionPause 
                        | PlaybackStateCompat.ActionStop);

                stateBuilder.SetState(state, mediaPlayer.CurrentPosition, 0, SystemClock.ElapsedRealtime());

                mediaSessionCompat.SetPlaybackState(stateBuilder.Build());

                //Used for backwards compatibility
                if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop) {
                    if (mediaSessionCompat.RemoteControlClient != null && mediaSessionCompat.RemoteControlClient.Equals(typeof(RemoteControlClient))) {
                        RemoteControlClient remoteControlClient = (RemoteControlClient) mediaSessionCompat.RemoteControlClient;

                        RemoteControlFlags flags = RemoteControlFlags.Play
                            | RemoteControlFlags.Pause
                            | RemoteControlFlags.PlayPause;

                        remoteControlClient.SetTransportControlFlags(flags);
                    }
                }

                OnStatusChanged (EventArgs.Empty);

                if (state == PlaybackStateCompat.StatePlaying || state == PlaybackStateCompat.StatePaused) {
                    StartNotification ();
                }
            }
            catch (Exception ex){
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// When we start on the foreground we will present a notification to the user
        /// When they press the notification it will take them to the main page so they can control the music
        /// </summary>
        private void StartNotification ()
        {
            if (mediaSessionCompat == null)
                return;

            var pendingIntent = PendingIntent.GetActivity (ApplicationContext, 0, new Intent (ApplicationContext, typeof(MainActivity)), PendingIntentFlags.UpdateCurrent);
            MediaMetadataCompat currentTrack = mediaControllerCompat.Metadata;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop) {
                Notification.MediaStyle style = new Notification.MediaStyle();
                style.SetMediaSession ((Android.Media.Session.MediaSession.Token)mediaSessionCompat.SessionToken.GetToken ());

                Notification.Builder builder = new Notification.Builder (ApplicationContext)
                    .SetStyle (style)
                    .SetContentTitle (currentTrack.GetString(MediaMetadata.MetadataKeyTitle))
                    .SetContentText (currentTrack.GetString(MediaMetadata.MetadataKeyArtist))
                    .SetContentInfo (currentTrack.GetString(MediaMetadata.MetadataKeyAlbum))
                    .SetSmallIcon (Resource.Drawable.album_art)
                    .SetLargeIcon (Cover as Bitmap)
                    .SetContentIntent (pendingIntent)
                    .SetVisibility (NotificationVisibility.Public)
                    .SetShowWhen(false)
                    .SetOngoing (MediaPlayerState == PlaybackStateCompat.StatePlaying);

                builder.AddAction( GenerateAction( Android.Resource.Drawable.IcMediaPrevious, "Previous", ActionPrevious ) );
                AddPlayPauseAction (builder);
                builder.AddAction( GenerateAction( Android.Resource.Drawable.IcMediaNext, "Next", ActionNext ) );
                builder.AddAction( GenerateAction( Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop", ActionStop ) );
                style.SetShowActionsInCompactView(0,1,2,4);

                NotificationManager.FromContext (ApplicationContext).Notify (NotificationId, builder.Build());
            } else {
                NotificationCompat.Builder builder = new NotificationCompat.Builder (ApplicationContext)
                    .SetContentTitle (currentTrack.GetString(MediaMetadata.MetadataKeyTitle))
                    .SetContentText (currentTrack.GetString(MediaMetadata.MetadataKeyArtist))
                    .SetContentInfo (currentTrack.GetString(MediaMetadata.MetadataKeyAlbum))
                    .SetSmallIcon (Resource.Drawable.album_art)
                    .SetLargeIcon (Cover as Bitmap)
                    .SetContentIntent (pendingIntent)
                    .SetShowWhen(false)
                    .SetOngoing (MediaPlayerState == PlaybackStateCompat.StatePlaying);

                builder.AddAction( GenerateActionCompat( Android.Resource.Drawable.IcMediaPrevious, "Previous", ActionPrevious ) );
                AddPlayPauseActionCompat (builder);
                builder.AddAction( GenerateActionCompat( Android.Resource.Drawable.IcMediaNext, "Next", ActionNext ) );
                builder.AddAction( GenerateActionCompat( Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop", ActionStop ) );

                NotificationManagerCompat.From (ApplicationContext).Notify (NotificationId, builder.Build());
            }
        }

        private Notification.Action GenerateAction( int icon, String title, String intentAction ) {
            Intent intent = new Intent(ApplicationContext, typeof(MediaPlayerService));
            intent.SetAction( intentAction );

            PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
            if (intentAction.Equals (ActionStop))
                flags = PendingIntentFlags.CancelCurrent;

            PendingIntent pendingIntent = PendingIntent.GetService(ApplicationContext, 1, intent, flags);

            return new Notification.Action.Builder (icon, title, pendingIntent).Build ();
        }

        private NotificationCompat.Action GenerateActionCompat( int icon, String title, String intentAction ) {
            Intent intent = new Intent(ApplicationContext, typeof(MediaPlayerService));
            intent.SetAction( intentAction );

            PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
            if (intentAction.Equals (ActionStop))
                flags = PendingIntentFlags.CancelCurrent;

            PendingIntent pendingIntent = PendingIntent.GetService(ApplicationContext, 1, intent, flags);

            return new NotificationCompat.Action.Builder (icon, title, pendingIntent).Build ();
        }

        private void AddPlayPauseAction(Notification.Builder builder) {
            if (MediaPlayerState == PlaybackStateCompat.StatePlaying)
                builder.AddAction (GenerateAction (Android.Resource.Drawable.IcMediaPause, "Pause", ActionPause));
            else
                builder.AddAction (GenerateAction (Android.Resource.Drawable.IcMediaPlay, "Play", ActionPlay));
        }

        private void AddPlayPauseActionCompat(NotificationCompat.Builder builder) {
            if (MediaPlayerState == PlaybackStateCompat.StatePlaying)
                builder.AddAction (GenerateActionCompat (Android.Resource.Drawable.IcMediaPause, "Pause", ActionPause));
            else
                builder.AddAction (GenerateActionCompat (Android.Resource.Drawable.IcMediaPlay, "Play", ActionPlay));
        }

        public void StopNotification ()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop) {
                NotificationManager nm = NotificationManager.FromContext (ApplicationContext);
                nm.CancelAll ();
            } else {
                NotificationManagerCompat nm = NotificationManagerCompat.From (ApplicationContext);
                nm.CancelAll ();
            }
        }

        /// <summary>
        /// Updates the metadata on the lock screen
        /// </summary>
        private void UpdateMediaMetadataCompat (MediaMetadataRetriever metaRetriever = null)
        {
            if (mediaSessionCompat == null)
                return;

            MediaMetadataCompat.Builder builder = new MediaMetadataCompat.Builder ();

            if (metaRetriever != null) {
                builder
                .PutString (MediaMetadata.MetadataKeyAlbum, metaRetriever.ExtractMetadata (MetadataKey.Album))
                .PutString (MediaMetadata.MetadataKeyArtist, metaRetriever.ExtractMetadata (MetadataKey.Artist))
                .PutString (MediaMetadata.MetadataKeyTitle, metaRetriever.ExtractMetadata (MetadataKey.Title));
            }
            builder.PutBitmap (MediaMetadata.MetadataKeyAlbumArt, Cover as Bitmap);

            mediaSessionCompat.SetMetadata(builder.Build());
        }

        [Obsolete ("deprecated")]
        public override StartCommandResult OnStartCommand (Intent intent, StartCommandFlags flags, int startId)
        {
            HandleIntent( intent );
            return base.OnStartCommand(intent, flags, startId);
        }

        private void HandleIntent( Intent intent ) {
            if( intent == null || intent.Action == null )
                return;

            String action = intent.Action;

            if( action.Equals( ActionPlay ) ) {
                mediaControllerCompat.GetTransportControls().Play();
            } else if( action.Equals( ActionPause ) ) {
                mediaControllerCompat.GetTransportControls().Pause();
            } else if( action.Equals( ActionPrevious ) ) {
                mediaControllerCompat.GetTransportControls().SkipToPrevious();
            } else if( action.Equals( ActionNext ) ) {
                mediaControllerCompat.GetTransportControls().SkipToNext();
            } else if( action.Equals( ActionStop ) ) {
                mediaControllerCompat.GetTransportControls().Stop();
            }
        }

        /// <summary>
        /// Lock the wifi so we can still stream under lock screen
        /// </summary>
        private void AquireWifiLock()
        {
            if (wifiLock == null){
                wifiLock = wifiManager.CreateWifiLock(WifiMode.Full, "xamarin_wifi_lock");
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

        private void UnregisterMediaSessionCompat ()
        {
            try {
                mediaSessionCompat.Dispose ();
                mediaSessionCompat = null;
            } catch (Exception ex) {
                Console.WriteLine (ex);
            }
        }

        IBinder binder;

        public override IBinder OnBind (Intent intent)
        {
            binder = new MediaPlayerServiceBinder (this);
            return binder;
        }

        public override bool OnUnbind (Intent intent)
        {
            StopNotification();
            return base.OnUnbind (intent);
        }

        /// <summary>
        /// Properly cleanup of your player by releasing resources
        /// </summary>
        public override void OnDestroy ()
        {
            base.OnDestroy ();
            if (mediaPlayer != null) {
                mediaPlayer.Release ();
                mediaPlayer = null;

                StopNotification ();
                StopForeground (true);
                ReleaseWifiLock ();
                UnregisterMediaSessionCompat ();
            }
        }

        /// <summary>
        /// For a good user experience we should account for when audio focus has changed.
        /// There is only 1 audio output there may be several media services trying to use it so
        /// we should act correctly based on this.  "duck" to be quiet and when we gain go full.
        /// All applications are encouraged to follow this, but are not enforced.
        /// </summary>
        /// <param name="focusChange"></param>
        public void OnAudioFocusChange (AudioFocus focusChange)
        {
            switch (focusChange) {
            case AudioFocus.Gain:
                if (mediaPlayer == null)
                    InitializePlayer ();

                if (!mediaPlayer.IsPlaying) {
                    mediaPlayer.Start ();
                }

                mediaPlayer.SetVolume (1.0f, 1.0f);//Turn it up!
                break;
            case AudioFocus.Loss:
                //We have lost focus stop!
                Stop ();
                break;
            case AudioFocus.LossTransient:
                //We have lost focus for a short time, but likely to resume so pause
                Pause ();
                break;
            case AudioFocus.LossTransientCanDuck:
                //We have lost focus but should till play at a muted 10% volume
                if (mediaPlayer.IsPlaying)
                    mediaPlayer.SetVolume (.1f, .1f);//turn it down!
                break;

            }
        }

        public class MediaSessionCallback : MediaSessionCompat.Callback {
            public override void OnPause ()
            {
                base.OnPause ();
            }

            public override void OnPlay ()
            {
                base.OnPlay ();
            }
        }
    }

    public class MediaPlayerServiceBinder : Binder
    {
        private MediaPlayerService service;

        public MediaPlayerServiceBinder (MediaPlayerService service)
        {
            this.service = service;
        }

        public MediaPlayerService GetMediaPlayerService ()
        {
            return service;
        }
    }
}
