using Android.App;
using Android.Content;
using Android.Media;
using BackgroundStreamingAudio.Services;

namespace BackgroundStreamingAudio.Receivers
{
    /// <summary>
    /// This is a simple intent receiver that is used to stop playback
    /// when audio become noisy, such as the user unplugged headphones
    /// </summary>
    [BroadcastReceiver]
    [IntentFilter(new []{AudioManager.ActionAudioBecomingNoisy})]
    public class MusicBroadcastReceiver: BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action != AudioManager.ActionAudioBecomingNoisy)
                return;

            //signal the service to stop!
            var stopIntent = new Intent(StreamingBackgroundService.ActionStop);
            context.StartService(stopIntent);
        }
    }
}