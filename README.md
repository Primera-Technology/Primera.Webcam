# Primera.Webcam

Primera.Webcam is a simple library for capturing images from connected, UVC compliant, camera devices on Windows via a [Microsoft Media Foundation (MMF)](https://learn.microsoft.com/en-us/windows/win32/medfound/microsoft-media-foundation-sdk) backend.

Due to the MMF dependency, the earliest supported OS version is Windows Vista.

## What and Why?

Capturing images from a webcam is a typical task in print applications, but existing workflows for capturing images require specific platforms, verbose interop, or are limited in the scope of their configuration.

Here we offer an alternative with slightly broader scope, but more granularity than similar options, explored below.

## Alternatives

### Open CV

[The Open CV library](https://docs.opencv.org/3.4/d0/da7/videoio_overview.html), and its many C# wrappers provide video capture functionality. This is likely to be a more powerful and flexible option for standard use.
Querying the capabilities, [e.g. available resolutions and formats](https://forum.opencv.org/t/how-get-all-resolution-of-a-webcam/6299/7), of a given capture device does not seem to be a supported feature of this popular library, however.

This library and CLI supports the querying of devices and their capabilities through MMF. In addition, all Media Foundation objects are thinly wrapped and available for direct consumption.

### UWP

[UWP](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/camera) has a camera integration on its platform. Unfortunately, that is an uncommon platform to target.

Instead, this library targets .NET Core-Windows and .NET Framework 4.7.2+ for compatibility. In addition, saving images and video to file is supported through the command line interface.

### Direct use of libraries

This library uses Microsoft Media Foundation for capturing images. The features within can be achieved through direct reference of MMF. The question of alternatives to MMF is better answered elsewhere, but a popular option is [DirectShow](https://learn.microsoft.com/en-us/windows/win32/directshow/directshow).
