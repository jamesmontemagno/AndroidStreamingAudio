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

        }

        private void SendAudioCommand(string action)
        {
            var intent = new Intent(action);
            StartService(intent);
        }

    }
}

