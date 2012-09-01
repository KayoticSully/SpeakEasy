using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using Microsoft.Kinect;
using Microsoft.Speech.Recognition;
using System.Threading;
using Microsoft.Speech.AudioFormat;
using System.Collections;

namespace SpeakEasy
{
    /*! 
     * \brief Main reflection engine for plguin translation.
     *
     * This is the main program that does the voice recognition,
     * maps the grammar down to the correct method in the correct
     * plugin, and executes the method.
     * 
     * \author Ryan Sullivan
     * \version 0.0.1
     * \date 04-19-2012
     * \copyright All Rights Reserved.
     */

    class Engine
    {
        private KinectSensor sensor; //<! The Kinect Sensor that has been attached to this engine
        private KinectAudioSource source; //!< The microphone from the Kinect.
        private static RecognizerInfo recogInfo; //<! Recognizer Information
        private List<Type> pluginTypes; //<! List of all plugin types from the plugin folder
        private Dictionary<string, Plugin> pluginsDict; //<! List of all plugin instances.
        private double confidenceFilter; //<! Threshold confidence has to pass to be considered "recognized"
        private SpeechRecognitionEngine speechRecogEngine; //<! The Microsoft Recognizer for Voice recognition

        /// \brief Main program.  For testing untill an overarching
        /// program is written.
        static void Main(string[] args)
        {
            new Engine();
        }

        /// \brief Speech Recognition and Reflection Engine
        /// 
        /// This is the main program object for the Speakeasy Engine.
        /// Please reference other documents on how this engine functions.
        public Engine()
        {
            bool setup = sensorInit();

            if (!setup)
            {
                Console.WriteLine("Press ENTER to exit.");
                Console.ReadLine();
                return;
            }

            loadPlugins("plugins");

            confidenceFilter = 0.75;

            startRecognition();
        }

        /// \brief Information about the voice recognizer
        public static RecognizerInfo RecognizerInfo
        {
            get
            {
                return recogInfo;
            }
        }

        /// \brief This loads plugin grammars into the recognizer,
        /// sets up callback methods,
        /// and starts speech recognition on the first loop through.
        private void startRecognition()
        {
            speechRecogEngine = new SpeechRecognitionEngine(recogInfo.Id);
            
            // load grammars
            foreach (Plugin plugin in pluginsDict.Values)
            {
                foreach (Grammar grammar in plugin.Grammars)
                {
                    speechRecogEngine.LoadGrammar(grammar);
                }
            }

            // hook handlers
            speechRecogEngine.SpeechRecognized += speechRecognizedHandler;
            speechRecogEngine.SpeechHypothesized += speechHypothesizedHandler;
            speechRecogEngine.SpeechRecognitionRejected += speechRejectedHandler;
            speechRecogEngine.SpeechDetected += speechDetectedHandler;
            speechRecogEngine.RecognizeCompleted += speechRecognizeCompleteHandler;

            using (Stream voiceStream = source.Start())
            {
                speechRecogEngine.SetInputToAudioStream(voiceStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                Console.WriteLine("Recognizing speech. Press ENTER to stop");

                speechRecogEngine.RecognizeAsync(RecognizeMode.Single);

                Console.ReadLine();
                Console.WriteLine("Stopping recognizer ...");

                speechRecogEngine.RecognizeAsyncStop();
            }

            sensor.Stop();
        }

        /// \brief loads plugins from directory and initalizes them
        /// 
        /// \param directory to load plugins from
        private void loadPlugins(String directory)
        {
            pluginTypes = getPluginsFromFolder("plugins");

            pluginsDict = new Dictionary<string, Plugin>();

            foreach (Type plugin in pluginTypes)
            {
                // create plugin
                Plugin pluginInstance = (Plugin)Activator.CreateInstance(plugin);
                // build Grammar
                pluginInstance.buildGrammar();
                // store plugin
                pluginsDict.Add(plugin.Name, pluginInstance);
            }
        }

        /// \brief Gets the Kinect sensor and initializes it
        /// 
        /// \return boolean. True if sensor is initalized, false otherwise.
        private bool sensorInit()
        {
            // Obtain a KinectSensor if any are available
            sensor = (from sensorToCheck in KinectSensor.KinectSensors where sensorToCheck.Status == KinectStatus.Connected select sensorToCheck).FirstOrDefault();
            if (sensor == null)
            {
                Console.WriteLine("Could not connect to Kinect.");
                return false;
            }

            Console.Write("Sensor Starting ... ");
            sensor.Start();
            Console.WriteLine("Sensor Ready");

            source = sensor.AudioSource; // Obtain the KinectAudioSource to do audio capture and set options
            source.EchoCancellationMode = EchoCancellationMode.CancellationAndSuppression; // No AEC
            source.AutomaticGainControlEnabled = false; // Important to turn this off for speech recognition

            recogInfo = GetKinectRecognizer();

            if (recogInfo == null)
            {
                Console.WriteLine("Could not find Kinect speech recognizer.");
                return false;
            }

            Console.WriteLine("Using: {0}", recogInfo.Name);

            // NOTE: Need to wait 4 seconds for device to be ready right after initialization
            int wait = 4;
            while (wait > 0)
            {
                Console.Write("Device will be ready for speech recognition in {0} second(s).\r", wait--);
                Thread.Sleep(1000);
            }

            Console.WriteLine(); // clear line
            return true;
        }

        /// \brief loads plugin assemblies from specified directory
        /// 
        /// \param[in] List<Type> a list of types of all plugins that were loaded
        private List<Type> getPluginsFromFolder(String path)
        {
            // get plugin directory
            DirectoryInfo dirInfo = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, path));
            DirectoryInfo[] pluginFolders = dirInfo.GetDirectories();

            List<Type> pluginTypeList = new List<Type>();

            // make sure it exists
            if (pluginFolders != null)
            {
                List<Assembly> pluginAssemblyList = new List<Assembly>();
                // go into each plugin directory
                foreach (DirectoryInfo directory in pluginFolders)
                {
                    // and get the main plugin dll
                    FileInfo[] files = directory.GetFiles(directory.Name+".dll");
                    FileInfo file = files[0];
                    Assembly assembly = Assembly.LoadFile(file.FullName);
                    pluginTypeList.AddRange(assembly.GetTypes().Where(type => typeof(PluginBase).IsAssignableFrom(type)));
                }
            }

            return pluginTypeList;
        }

        /// \brief gets the Speech Recognizer Information
        /// 
        /// \returns RecognizerInfo
        private RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
            };

            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

        /// \brief Handles positive recognition events
        private void speechRecognizedHandler(object sender, SpeechRecognizedEventArgs e)
        {
            
            Console.Write("\rCommand Recognized: \t{0}\t\tConfidence:\t{1}\n", e.Result.Text, e.Result.Confidence);

            if (e.Result.Confidence >= confidenceFilter)
            {
                doCommand(e.Result);
            }
        }

        /// \brief Handles Speech Hypothesis Events
        private void speechHypothesizedHandler(object sender, SpeechHypothesizedEventArgs e)
        {
            Console.Write("\rCommand Hypothesized: \t{0}\t\tConfidence:\t{1}", e.Result.Text, e.Result.Confidence);
        }

        /// \brief Handles Speech Rejection Events
        private void speechRejectedHandler(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.Write("\rCommand Rejected: \t{0}\t\tConfidence:\t{1}\n", e.Result.Text, e.Result.Confidence);
        }

        /// \brief Handles Speech Detected Events
        private void speechDetectedHandler(object sender, SpeechDetectedEventArgs e)
        {
            // not sure what this is for.
        }

        /// \brief Handles Recognition Complete Events
        private void speechRecognizeCompleteHandler(object sender, RecognizeCompletedEventArgs e)
        {
            if(!e.Cancelled)
                speechRecogEngine.RecognizeAsync(RecognizeMode.Single);
        }

        /// \brief Casts all elements in a Dictionary to supplied Type.
        /// 
        /// This is necessary in the command translation process, but can be
        /// used anywhere this is needed.
        public static IEnumerable<DictionaryEntry> CastDict(IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return entry;
            }
        }

        /// \brief Translates recognized command to plugin method, then Executes.
        private void doCommand(RecognitionResult speechResult)
        {
            string[] metaData = speechResult.Grammar.Name.Split(':');

            // plugin to look in
            string className = metaData[0];
            // unique method - for overloading support
            string methodUniqueName = metaData[1];
            // method name - for invoking
            string methodName = methodUniqueName.Split('-')[0];
            // method words - for filtering
            int methodParts = methodName.Split('_').Count();

            PluginBase plugin = pluginsDict[className];
            ParameterInfo[] parameterInfos = plugin.MethodParameterInfo[methodUniqueName];

            int paramIndex = 0;
            List<object> parameterList = new List<object>();

            List<Type> parameterTypes = new List<Type>();
                                                                                               // skip activation word + method words
            foreach (RecognizedWordUnit word in speechResult.Words.Where(word => word.Text != "...").Skip(1 + methodParts))
            {
                // add type for method init
                parameterTypes.Add(parameterInfos[paramIndex].ParameterType);
                ParameterInfo parameter = parameterInfos[paramIndex++];
                // if Enum
                if (parameter.ParameterType.IsEnum)
                {
                    parameterList.Add(Enum.Parse(parameter.ParameterType, word.Text));
                }
                else
                {
                    // get param name
                    string paramName = parameter.Name;

                    // get corrosponding Dictionary
                    IDictionary tempDict = (IDictionary)plugin.GetType().GetField(paramName).GetValue(plugin);
                    Dictionary<string, object> paramDict = CastDict(tempDict).ToDictionary(entry => (string)entry.Key,
                                                                                           entry => entry.Value);
                    dynamic realParam = paramDict[word.Text];
                    parameterList.Add(realParam);
                }
            }

            object[] parameters;
            if (parameterList.Count == 0)
                parameters = null;
            else
                parameters = parameterList.ToArray();

            if (parameterInfos == null)
                plugin.GetType().GetMethod(methodName).Invoke(plugin, parameters);
            else
                plugin.GetType().GetMethod(methodName, parameterTypes.ToArray()).Invoke(plugin, parameters);
        }
    }
}