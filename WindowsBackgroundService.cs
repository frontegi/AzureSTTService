using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace STTCloudService
{
    public class WindowsBackgroundService : BackgroundService
    {
        private readonly ILogger<WindowsBackgroundService> _logger;

        private static string STTKey;
        private static string STTRegion;
        private static string STTSourceLanguage; 

        private static string InputFolder; 
        private static string OutputFolder;
        private static string ErrorFolder;
        private static string ChannelsSplitFolder;
        private static string SingleTxtTranslationTargetFile;
        

        private static SpeechConfig speechConfig;

        public WindowsBackgroundService(ILogger<WindowsBackgroundService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting STT service, made in Italy by Giulio Fronterotta");
            //Inject the configuration file in memory
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, true);

            var config = builder.Build();

            //Extract values from Application Configuration file
            STTKey = config["AzureSTTService:Key"];
            STTRegion = config["AzureSTTService:Region"];
            STTSourceLanguage = config["AzureSTTService:SourceLanguage"]; 

            InputFolder = config["Folders:InputFolder"];
            OutputFolder = config["Folders:OutputFolder"];
            ErrorFolder = config["Folders:ErrorFolder"];
            ChannelsSplitFolder = config["Folders:ChannelsSplitFolder"];
            SingleTxtTranslationTargetFile = config["Folders:SingleTxtTranslationTargetFile"];

            //Create directories if they not exits
            Directory.CreateDirectory(InputFolder);
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(ErrorFolder);
            Directory.CreateDirectory(ChannelsSplitFolder);

            speechConfig = SpeechConfig.FromSubscription(STTKey, STTRegion);
            _logger.LogInformation("STT Service Started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //Search Wav Files
                    var WavFileList = Directory
                        .EnumerateFiles(InputFolder, "*.wav")
                        .OrderByDescending(x => File.GetCreationTime(x));
                    //Log if any file found
                    
                    if (WavFileList.Count() > 0) 
                    {
                        string FileListWithLineFeed = String.Join("\r\n", WavFileList);
                        _logger.LogInformation(
                            String.Format("Found {0} file(s) to process, from oldest to newest\r\n{1}", 
                            WavFileList.Count(), 
                            FileListWithLineFeed)
                            );
                        _logger.LogInformation("Batch file processing started");
                        
                        //Loop through file for processing
                        foreach (var wavFile in WavFileList)
                        {
                            await ProcessFileAsync(wavFile, STTSourceLanguage, OutputFolder, ErrorFolder, ChannelsSplitFolder, SingleTxtTranslationTargetFile);
                        }
                        _logger.LogInformation("Batch file processing finished");
                    } 
                   
                }
                catch (Exception exc)
                {
                    _logger.LogError(exc.ToString());
                    throw;
                }    
                //Set Polling Time
                await Task.Delay(5000, stoppingToken);
            }
            _logger.LogInformation("Service Stopped");
        }

        private async Task ProcessFileAsync(string wavFile,string sourceLanguage, string destDir, string errorDir,string channelSplitFolder,string singleTxtTranslationTargetFile)
        {
            _logger.LogInformation(String.Format("Detected new file {0}", wavFile));           
            
            string sourcePath = wavFile;
            string prefixTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_");
            string destWavPath = Path.Combine(destDir, prefixTimestamp + Path.GetFileName(wavFile));
            string destSttTxtPath = Path.Combine(destDir, prefixTimestamp + Path.GetFileNameWithoutExtension(wavFile) + ".txt");
            string destErrorFilePath = Path.Combine(errorDir, prefixTimestamp + Path.GetFileName(wavFile));

            Boolean channelSplit = false;
            Boolean sampleRateConversion = false;


            using (var reader = new WaveFileReader(sourcePath)) {
                //Separate Channels


                _logger.LogInformation(String.Format($"Found {reader.WaveFormat.Channels} channels"));
                _logger.LogInformation(String.Format($"Bits Per Sample: {reader.WaveFormat.BitsPerSample}"));
                _logger.LogInformation(String.Format($"Sample Rate: {reader.WaveFormat.SampleRate}"));

                //Clean Audio Processing directory
                string[] filePaths = Directory.GetFiles(channelSplitFolder);
                foreach (string filePath in filePaths)
                    File.Delete(filePath);

                //Split only multiple channels files (more than 2 stereo channels)
                if (reader.WaveFormat.Channels > 2)
                {
                    //Tag bool to notify split
                    channelSplit = true;

                    var writers = new WaveFileWriter[reader.WaveFormat.Channels];
                    for (int n = 0; n < writers.Length; n++)
                    {
                        var format = new WaveFormat(reader.WaveFormat.SampleRate, 16, 1);
                        string combinedPath = Path.Combine(channelSplitFolder, string.Format($"channel-{n + 1}.wav"));
                        writers[n] = new WaveFileWriter(combinedPath, format);
                    }

                    float[] buffer;
                    while ((buffer = reader.ReadNextSampleFrame())?.Length > 0)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            // write one sample for each channel (i is the channelNumber)
                            writers[i].WriteSample(buffer[i]);
                        }
                    }

                    for (int n = 0; n < writers.Length; n++)
                    {
                        writers[n].Dispose();
                    }
                    //reader.Dispose();
                }

                //Check if I need to downsample Files
                if (reader.WaveFormat.SampleRate > 16000)
                {
                    sampleRateConversion = true;
                    if (channelSplit == true)
                    {
                        //If a previous channel split has been performed...
                        foreach (string file in Directory.EnumerateFiles(channelSplitFolder))
                        {
                            //Convert file format with NAudio
                            //https://stackoverflow.com/questions/6647730/change-wav-file-to-16khz-and-8bit-with-using-naudio

                            string convertedStreamFilename = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "-16KHz.wav");

                            using (var wavConverterReader = new WaveFileReader(file))
                            {
                                var newFormat = new WaveFormat(16000, 16, 1);

                                using (var conversionStream = new WaveFormatConversionStream(newFormat, wavConverterReader))
                                {
                                    //Replace original file
                                    WaveFileWriter.CreateWaveFile(convertedStreamFilename, conversionStream);
                                }
                            }

                        }

                    }
                    else
                    {
                        //Else I need to convert the rate of a single file
                        string convertedStreamFilename = Path.Combine(Path.GetDirectoryName(sourcePath), "channel-1-16KHz.wav");
                        using (var wavConverterReader = new WaveFileReader(sourcePath))
                        {
                            var newFormat = new WaveFormat(16000, 16, 1);

                            using (var conversionStream = new WaveFormatConversionStream(newFormat, wavConverterReader))
                            {
                                //Replace original file
                                WaveFileWriter.CreateWaveFile(convertedStreamFilename, conversionStream);
                            }
                        }
                    }
                }
            }

            

            //This will be changed depending on type of file conversions required
            string STTSourcePath = wavFile;

            //Source path to submit to cloud is selected, depending on transformation previously performed.
            if (channelSplit == true && sampleRateConversion == true) STTSourcePath = Path.Combine(channelSplitFolder, string.Format("channel-1-16KHz.wav"));
            if (channelSplit == true && sampleRateConversion == false) STTSourcePath = Path.Combine(channelSplitFolder, string.Format("channel-1.wav"));
            if (channelSplit == false && sampleRateConversion == true) STTSourcePath = Path.Combine(channelSplitFolder, string.Format("channel-1-16KHz.wav")); ;
            if (channelSplit == false && sampleRateConversion == false) STTSourcePath = sourcePath;
            
            
            //Perform STT
            using AudioConfig audioConfig = AudioConfig.FromWavFileInput(STTSourcePath);

            speechConfig.SpeechRecognitionLanguage = sourceLanguage;
            speechConfig.EnableAudioLogging();
            speechConfig.SetProperty(PropertyId.Conversation_Initial_Silence_Timeout, "5");

            ResultReason STTResult;
            using (var recognizer = new SpeechRecognizer(speechConfig, audioConfig))
            {
                _logger.LogInformation(String.Format("Sending file {0} to Azure STT Service", Path.GetFileName(STTSourcePath)));
                //Do the real STT Job on cloud
                var result = await recognizer.RecognizeOnceAsync();
                STTResult = result.Reason;
                if (result.Reason == ResultReason.RecognizedSpeech) {                    
                    _logger.LogInformation("Moving file from {0}\r\nto {1}", sourcePath, destWavPath);
                    _logger.LogInformation($"RECOGNIZED STT: Text={result.Text}");
                    //Dumping Json Response file to History Directory
                    await File.WriteAllTextAsync(destSttTxtPath, result.Text);
                    //Dumping Json Response file to fixed destination directory (This one is not really required, but introduced for a specific need)
                    await File.WriteAllTextAsync(singleTxtTranslationTargetFile, result.Text); 
                }
                //Avoid File locking errors
                recognizer.Dispose();
            }

            if (STTResult == ResultReason.RecognizedSpeech) {
                //Move File to destination
                File.Move(sourcePath, destWavPath);
                _logger.LogInformation("File Moved");
            }
            else
            {
                _logger.LogError(String.Format("Error Processing {0} with Azure STT Service.\r\n ERROR:{1}", Path.GetFileName(wavFile), STTResult));
                //Encountered Errors
                //Move File to error Folders
                File.Move(sourcePath, destErrorFilePath);
                _logger.LogError(String.Format("Error Processing {0} with Azure STT Service.\r\n ERROR:{1}", Path.GetFileName(wavFile), STTResult));
            }

           
        }
    }
}