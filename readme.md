# Azure STT Windows Background Service

This project aim is to create a simple Windows Background service in C# code, to perform subsequent Speech-To-Text (STT) process  using Azure cloud, on incoming wave file(s), placed in a specific windows directory.

## Disclaimer 1
This software works but it's not PRODUCTION READY.
## Disclaimer 2
Please read the whole guide until end, you need each detail before starting the service, otherwise you'll become crazy trying to figure out what's wrong.

## Description
The code can be launched/debugged from Visual Studio. When you want to package the binaries, you can use the Project Publish -> Publish to Folder feature function to create the deployable version. You can create a self-contained exe package, with all the required .Net core file embedded (portable). You can run this service even on a windows machine without .Net Core framework (obviously, the file has a huge size). 

If you want a lighter version and .net Core framework is installed on target machine, use the classic Binary Relese created during compilation.

# Libraries used
**.Net core 5.0**

This project has been built with .Net Core 5.0 framework. 
Please refer to (https://docs.microsoft.com/en-us/dotnet/core/extensions/windows-service) for information on developing .Net Core 5.0 Background services.

**Microsoft Cognitive Services Speech SDK**

To interact with cloud Speech services, this software use the c# Speech SDK library from Microsoft.
For further info and documentation,go to (https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/speech-sdk)

**NAudio**

Due to a specific requirements, processing and transforming wave files BEFORE the submission to the Azure Cloud Speech Services, the service make use of the library NAudio, that sadly requires the service to be built/targeted on Windows platform only. 
(https://github.com/naudio/NAudio)

# How to Create an Azure Speech service resource
Azure gives you 5 hours of free Speech-To-Text.
To create your Speech Service resource, follow these steps (Prerequisite, you have at least a trial Azure Subscription):

1.	Visit this URL, https://portal.azure.com/#blade/HubsExtension/BrowseResource/resourceType/Microsoft.CognitiveServices%2Faccounts
2.	Press the button "Add Speech Service"

![Add Speech Service](/images/tutorial_01.png)

3.	Fill the values as in the below example. **Name** is whatever you want. **Subscription** is your Azure Subscription. **Location** is the Azure region where your service will be hosted. **Pricing Tier** can be F0, the free one, but you can create only one free resource for each subscription. S0 is the paid one, but you have always 5 free hour before paying. As **Resource Group** select an existing one or create a fresh resource group right here.

![Add Speech Service](/images/tutorial_02.png) 

4.	Press "Create"
5.	At the end of deployment, press “Go To Resource”, or open the newly created resource from the specific Resource Group

![Add Speech Service](/images/tutorial_03.png) 
 
6.	Read the Key to be able to use the service. Remember, this is the setting to be placed in the Application Settings file, BEFORE starting the service

![Add Speech Service](/images/tutorial_04.png) 

If you better prefer, take a look at the official Microsoft Guide to create the Speech service (https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/overview#create-the-azure-resource)

# Installation

Once the project is built (or published to a folder), copy the binaries output and place them in a directory of your choice (ex: "c:\Program Files\STTCloudservice")

To install it as a windows service
* open a powershell as administrator 
* run the following command, replace **binpath** with your binaries folder

```bash
sc.exe create "Azure Cloud STT Service" binpath=C:\stt-service\AzureCloudSTTService.exe
```

## Interact with the windows service
You can start and stop service from the Services Consoles (WIN+R then launch "services.msc")

Alternatively, from the command line

* Start Service 

```bash
sc.exe start "Azure Cloud STT Service"
```

* Stop Service 

```bash
sc.exe stop "Azure Cloud STT Service"
```

## Uninstall

Before uninstall, please stop the service using above command.
then launch

```bash
sc.exe delete "Azure Cloud STT Service"
```


# Folders structure
The below structure is only an example. Each folder can be in a different path in your machine.

```
root 
│   
└───inputFolder 
[The folder where you insert wave files to be elaborated]

└───channelSplitFolder 
[A temporary working folder. When a wave file has more than 2 channels or it has been recorded at a frequency higher than 16 Khz, here you will find the downsampled version and individual channels (i.e. a 4 channels wave file will be splitted in 4 different mono file)]

└───errorFolder
[if something unexpected happens during the process, the wave file is moved from inputFolder to errorFolder, to avoid reprocessing of the file]

└───outputFolder
[at the end of processing wave file is moved from the input folder in the output folder renamed with a timestamp. A corresponding txt file is created along the wave file with the transcription]
```

#  Application Settings
**Before starting the service**, please change the application settings, at your convenience.
Open appsettings.json with yor favourite editor.

Keys description:

* **AzureSTTService - Key** : Place your speech service secret key

* **AzureSTTService - Region** : Set the region where you provisioned the service (i.e.= westeurope)

* **AzureSTTService - SourceLanguage** : Set the language to transcribe from. For a complete list goto column **Locale (BCP-47)** at (https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support#speech-to-text) 

DO NOT FORGET THE DOUBLE SLASHES IN THE PATHS


* **Folders - InputFolder** : The folder where you insert wave files to be elaborated (i.e c:\\stt\\inputFolder)


* **Folders - OutputFolder** : Where you will find original file wav and txt transcription renamed with a timestamp


* **Folders - ErrorFolder** : If any error occur, file are moved here from the input folder


* **Folders - ChannelsSplitFolder** : Post-Processed audio file (stereo or more channels, frequencies higher than 16 Khz) are placed here for convenience. Directory is cleaned at each transcription.

* **Folders - SingleTxtTranslationTargetFile** : A path to a specific txt file. It will always contain the latest transcription and will be overwritten by each processing iteration (i.e "c:\\stt\\outputfolder\\last_transcription.txt")

#  Logging
Classic logging is used, you can find any trace in the windows events consolle.
To open (WIN+R -> eventvwr.msc -> OK)
or search Event Viewer in the Start Menu

#  How to contribute
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License
[Boost Software License](https://choosealicense.com/licenses/bsl-1.0/)