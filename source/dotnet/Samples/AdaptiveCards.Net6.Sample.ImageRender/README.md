# AdaptiveCards Image Renderer Sample

This Windows CLI exercises `AdaptiveCards.Rendering.Wpf.Net6` on .NET 10
(`net10.0-windows`), retaining the existing .NET 6 compatibility target.

## Usage

If you cloned this repo, run the following command to generate images from the Scenario samples. The tool scans a directory for .json files that contain Adaptive Card payloads.

```console
Usage: dotnet run -f net10.0-windows -p:PublicBuild=true --no-launch-profile -- [payload-path] [options]
```

```console
dotnet run -f net10.0-windows -p:PublicBuild=true --no-launch-profile -- ..\..\..\..\samples\v1.0\Scenarios -o out
```

Or output to a specific directory

```console
dotnet run -f net10.0-windows -p:PublicBuild=true --no-launch-profile -- ..\..\Test\RenderingSmoke\card.json -i -o out
```


### Arguments

Name | Description
---|---
path | The path that contains JSON card payloads

### Options

Short | Long | Description
---|---|---
-h |--help | Show help information
-r |--recursive | Recurse the directory for all JSON files
-o |--out | The directory to output the image(s) to
-i | --supports-interactivity  | Include actions and inputs in the output
n/a | --host-config | Specify a host config file

## Example to use Windows Notification host config

```console
dotnet run -f net10.0-windows -p:PublicBuild=true --no-launch-profile -- ..\..\..\..\samples\v1.0\Scenarios -r -i -o out --host-config ..\..\..\..\samples\hostconfig\windows-notification.json
```

Commands above run from this sample directory. Invalid/missing input, empty
directories and rendering failures return a nonzero process exit code.

`PublicBuild=true` explicitly builds an unsigned, non-packable local WPF
assembly because Microsoft's release signing key is not in this checkout.
It must not be published as a release binary. See the
[.NET README](../../README.md#windows-desktop-and-signing) for the signing boundary
and modern/compatibility validation commands.