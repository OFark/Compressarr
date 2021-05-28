
# Compressarr
A tool that integrates with Radarr and Sonarr and uses FFmpeg to recompress items from your library, with your options or calculated ones.
 
## Source
Radarr and Sonarr items can be filtered to a selection and then processed. The Job remembers the filter not the files so any new files that match the filter can be processed later. 
 
## FFmpeg
Presets are defined to tell FFmpeg what to do with each file from the filter. This is complete control over FFmpeg. Audio, Video, mapping, any codec installed on your computer you can use. The only automated thing is 2 pass on fixed bitrate, otherwise you should be able to do anything that FFmpeg can do. There are some options that Compressarr can use to enable best-guess calculation to maintain picture quality and still compress the file.

 
 ### Instructions:
  1. You need to specify a Radarr URL and API key on the options page. 
  2. You need to specify one or more Filters on the Radarr and/or Sonarr pages. This can be achieved by selecting items from the drop down options at the top. You can also double click column headers to filter by that column. Save your Filter and give it a name.
  3. You may need to define some FFmpeg Presets on the FFmpeg page. There are some examples included. This can be quite complicated or quite easy depending on what you want to achieve. First a name, then pick the container for the output (e.g. Matroska), then a video codec (e.g. [libx265](https://trac.ffmpeg.org/wiki/Encode/H.265)) and an audio codec, or leave that to copy. Once you've picked a codec, codec options will appear, you can create your own codec options if you need to. Simple option is to set the new video bit rate, however if you are using a libx codec you can leave the bit rate and choose a [CRF](https://trac.ffmpeg.org/wiki/Encode/H.265) value, a Preset and a Tune if you wish. Recommend Slow and Fast Decode. You can then see at the bottom exactly what will be passed to FFmpeg once your file names have been inserted.
  4. Finally you need to create a/some jobs. This is done on the Jobs screen. This screen will also guide you if you've missed a step. Select one of your Filters, select a FFmpeg Preset, Enter a Base folder and a Destination folder. You can then choose to tell Radarr/Sonarr to auto import based on some checks if you wish. [SSIM](https://en.wikipedia.org/wiki/Structural_similarity) is a visual check to make sure the original file and the new file are similar. An automatic video length check is done, you can also make sure the new file is smaller then the original by a percentage too. Once created and tested these jobs can be started to start the process and encoding.

A note on Base folder and Destination folder:
These have to be accessible to Radarr/Sonarr. Base folder allows you to set a starting point for your files, most of the time this may be blank Compressarr will then just append whatever is in Radarr/Sonarr's file paths to that. If you get it wrong, the Job Test will show you what it's looking for, so you should be able to work out what's going on.
The Destination folder is critical for Radarr to be able to see IF you want to auto import. Basically Compressarr passes the full path to Radarr/Sonarr during the file import. 

#### Auto Import
This will tell Radarr/Sonarr that there's a new video to import, with a full path to the file. Failure will not stop the next file being encoded. Upon the successful encoding of a video, you can elect to check for [SSIM](https://en.wikipedia.org/wiki/Structural_similarity) and Compression levels. [SSIM](https://en.wikipedia.org/wiki/Structural_similarity) is a minimum that the encode must reach to be imported, Compression is a maximum allowed file size, compared to the original, as a percentage. So 95% and 100%, respectively, are reasonable values.

#### Auto Calculation
This option allows Compressarr to go through each of the options available for testing in the profile for the encoder. So for example you can tell it to go through each of the constant quality options to see which gets closest to the original video whilst still coming in under file size. It uses a sample of the video, the length of which you can choose in options, and is switched on where you see the calculator icon on the profile page.

### Installation: 
[Dotnet Core 5](https://dotnet.microsoft.com/download/dotnet/5.0) runtimes are required, and are provided in Docker.
#### Windows:
Run Compressarr.exe, that's about it.
#### Linux
Running Compressarr should be enough.
#### Mac
I have no idea, all I know is .Net core is supposed to run on Mac
#### Docker
This is going to require a little config. First you'll need to make sure you mapped the same volumes as Radarr/Sonarr have. (See notes above) So that the path's match when passed back and forth. You will also need to supply the NVIDIA_VISIBLE_DEVICES ID of the GPU you want to use for the Nvidia Cuda branch (:nvidia). See: [Docs](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/user-guide.html) for more info.

On launch the application will download the latest version of FFmpeg. 

### Configuration:
All configuration that you need to change is in the UI, however in the config folder in the app (/config in Docker) you'll find some configuration files. appsettings.json allows you to configure [log levels](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line), log directory, and log outputs (File, console, debugger). 
Also there's a folder called CodecOptions. In here are some templates for configuring Codecs. They specify what additional options are available for a codec. They are case sensitive to the codec name and must end .json. All options available are in use in the existing examples.
 
### Known issues:

 - Currently Sonarr will connect, but there's no interface to build a filter.

### And Finally:

 This process rebuilds your videos, if you choose to auto import it will over write your existing files, if that what Radarr/Sonarr are set to do. Please check that the output is what you want before enabling the Auto Import option.