# amsplayground


A console application demonstrating the Media Services NET SDK.

- File Upload
- Encode to Multibitrate MP4 with progress indicator
- Get streaming locator

Amend `app.config` with your AMS account name and key:

```xml
  <appSettings>
    <add key="accKey" value="MEDIA_SERVICES_ACCOUNT_KEY" />
    <add key="accName" value="MEDIA_SERVICES_ACCOUNT_NAME" />
  </appSettings>
```

## Resources

### Media Services SDK for PHP
https://github.com/Azure/azure-sdk-for-PHP


### Task Presets (encoding profiles) for Media Encoder Standard
https://msdn.microsoft.com/en-us/library/azure/mt269960.aspx


### Building an On-Demand Video Service with Microsoft Azure Media Services(Microsoft Patterns & Practices)
https://msdn.microsoft.com/en-us/library/dn735912.aspx


### Filters and Dynamic Manifest
https://azure.microsoft.com/en-gb/documentation/articles/media-services-dynamic-manifest-overview/


### Media Services Extensions
https://github.com/Azure/azure-sdk-for-media-services-extensions
http://mingfeiy.com/announcing-windows-azure-media-services-extension-sdk-version-2-0


### Azure Media Services Explorer (AMSE)
http://aka.ms/amse


### Azure Media Player (any browser, any device)
http://ampdemo.azureedge.net/azuremediaplayer.html


### Azure Media Player Documentation
http://amp.azure.net/libs/amp/latest/docs/
