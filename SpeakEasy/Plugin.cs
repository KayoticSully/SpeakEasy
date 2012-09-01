using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Speech.Synthesis;
using Microsoft.Speech.Recognition;
using iSpeech;
using System.Media;

namespace SpeakEasy
{
    /*! 
     * \brief Helper class for plugins to inherit.
     *
     * Provides many helpful functions to plugin classes, developers
     * can either inherit from this class or PluginBase.  Please
     * read \ref PluginCreation "How to create a Plugin" page in
     * the documentation.
     * 
     * \author Ryan Sullivan
     * \version 0.0.1
     * \date 04-19-2012
     * \copyright All Rights Reserved.
     */

    public class Plugin : PluginBase
    {
        private static string ispeech_api = "developerdemokeydeveloperdemokey";//"cbdb58a0bfb5e66113a96381e26fd484"; //!< iSpeech Web API Key
        private static bool ispeech_production = false; //!< iSpeech production switch
        private static iSpeechSynthesis synth; //!< iSpeech synthesizer.  Static so only one exists for all Plugins.
        private bool ispeech_enabled = false; //!< iSpeech toggle. Instance so each plugin can change this.
        private string ispeech_voice; //!< iSpeech voice setting for each plugin.

        /// \brief Constructor sets up iSpeech. No need to call it.
        public Plugin()
        {
            Console.WriteLine("From Plugin");

            if (synth == null)
            {
                synth = iSpeechSynthesis.getInstance(ispeech_api, ispeech_production);
                synth.setOptionalCommands("format", "wav");
            }
        }
        
        /// \brief the iSpeech voice that will be used.
        /// 
        /// Every plugin can have its own unique voice if
        /// setup that way.  Keep in mind later on users will
        /// be able to override developer-set voices for ones
        /// they rather.  But whatever voice you set, will be the
        /// default for your plugin.
        protected string Voice
        {
            get
            {
                return ispeech_voice;
            }

            set
            {
                ispeech_voice = value;
            }
        }

        /// \brief enable or disable the iSpeech Synthesis SDK
        /// 
        /// Set to false to disable iSpeech.  This is true by default;
        protected bool ISpeechEnabled
        {
            get
            {
                return ispeech_enabled;
            }

            set
            {
                ispeech_enabled = value;
            }
        }

        /// \brief Causes the computer to "Speak" the supplied string using text to speech.
        /// 
        /// This, by default uses the built-in Microsoft Anna voice, but can be adapted to work
        /// with other text to speech api's.
        /// 
        /// \param[in] phrase string to be spoken.
        /// 

        protected void speak(string phrase)
        {
            if (ispeech_enabled)
            {
                synth.setVoice(ispeech_voice);
                TTSResult result = synth.speak(phrase);
                SoundPlayer player = new SoundPlayer(result.getStream());
                player.PlaySync();
            }
            else
            {
                using (SpeechSynthesizer synth = new SpeechSynthesizer())
                {
                    synth.Speak(phrase);
                }
            }

        }

        /// \brief Allows new start commands to be added for commands within a plugin.
        /// 
        /// By default the start-commands "Kinect", "Windows", and "Computer" are added to
        /// every grammar.  These are the phrases that must precede a command to "activate"
        /// the engine.  This method allows for additional start phrases to be defined for
        /// the commands in that plugin only.
        /// 
        /// \param[in] initPhrases List of strings to be added as start commands.
        protected void addInitPhrases(List<string> initPhrases)
        {
            startCommands = startCommands.Concat(initPhrases).ToArray();
        }


        /// \brief Defines the only start commands for commands within a plugin.
        /// 
        /// This method overrides the default phrases to make sure only the supplied
        /// phrases can be used for commands within the plugin
        /// 
        /// \param[in] initPhrases List of strings to be used as start commands.
        protected void onlyInitPhrases(List<string> initPhrases)
        {
            startCommands = initPhrases.ToArray();
        }

       
        // UNDOCUMENTED

        private string getPluginFolder()
        {
            string pluginFile = System.IO.Path.GetFileName(System.Reflection.Assembly.GetCallingAssembly().Location);
            return pluginFile.Split('.')[0];
        }

        protected string getPluginFilePath()
        {
            string pluginGroup = getPluginFolder();
            return "plugins/" + pluginGroup + "/";
        }

        protected string getSubPluginFilePath()
        {
            string pluginGroup = getPluginFolder();
            return "plugins/" + pluginGroup + "/" + this.GetType().Name + "/";
        }

        protected string loadFileToString(string fileName)
        {
            string filePath = getPluginFilePath() + fileName;
            return System.IO.File.ReadAllText(filePath);
        }

        protected IEnumerable<string> loadFileToArray(string fileName)
        {
            string filePath = getPluginFilePath() + fileName;
            return System.IO.File.ReadLines(filePath);
        }
    }
}

/// \page PluginCreation How to create a plugin.
/// \section Basic Setup
/// Creating a plugin is rather simple (as it was designed to be).
/// There are a few things that you need to do.
/// \par 1. Create Project of type \c Class \c Library in Visual Studio.
/// 
/// \par 2. Add reference to this project.
/// 
/// \par 3. Add \c using \c SpeakEasy to your usings block
///
/// \par 4. Extend Plugin or PluginBase
/// 
/// As long as thoes 4 steps are done, you are all set up.  A base class is provided
/// below for reference.
/// 
/// \note 
/// Keep in mind you can inherit either Plugin or PluginBase.  The
/// only difference is Plugin includes extra tools to use while PluginBase
/// is only the bare-bones engine.
/// 
/// \code
/// using System;
/// using System.Collections.Generic;
/// using System.Linq;
/// using System.Text;
/// using Microsoft.Speech.Recognition;
/// using SpeakEasy;

/// namespace PluginNamespace
/// {
///     public class PluginName : Plugin // or PluginBase
///     {
///         public void ping()
///         {
///             speak("Pong");
///         }
///     }
/// }
/// \endcode