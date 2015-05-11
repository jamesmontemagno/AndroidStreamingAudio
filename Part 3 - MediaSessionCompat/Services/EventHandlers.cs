using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using BackgroundStreamingAudio.Services;
using System;

namespace BackgroundStreamingAudio
{
	public delegate void StatusChangedEventHandler (object sender, EventArgs e);

    public delegate void BufferingEventHandler(object sender, EventArgs e);

    public delegate void CoverReloadedEventHandler(object sender, EventArgs e);

    public delegate void PlayingEventHandler(object sender, EventArgs e);
}

