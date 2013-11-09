using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using BackgroundStreamingAudio.Services;

namespace BackgroundStreamingAudio
{
    [Activity(Label = "BackgroundStreamingAudio", MainLauncher = true, Icon = "@drawable/ic_launcher", Theme = "@style/Theme")]
    public class MainActivity : Activity
    {

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            var play = FindViewById<Button>(Resource.Id.playButton);
            var pause = FindViewById<Button>(Resource.Id.pauseButton);
            var stop = FindViewById<Button>(Resource.Id.stopButton);

            play.Click += (sender, args) => SendAudioCommand(StreamingBackgroundService.ActionPlay);
            pause.Click += (sender, args) => SendAudioCommand(StreamingBackgroundService.ActionPause);
            stop.Click += (sender, args) => SendAudioCommand(StreamingBackgroundService.ActionStop);

			var musicBy = FindViewById<TextView> (Resource.Id.musicbytext);
			musicBy.Clickable = true;
			musicBy.Click += (sender, e) => {
				var musicByIntent = new Intent(Intent.ActionView);
				musicByIntent.SetData(Android.Net.Uri.Parse("http://freemusicarchive.org/music/Raw_Stiles/STOP_2X2_04/A2_Raw_Stiles_-_Rouge_1291"));
				StartActivity(musicByIntent);
			};

        }

        private void SendAudioCommand(string action)
        {
            var intent = new Intent(action);
            StartService(intent);
        }

    }
}

