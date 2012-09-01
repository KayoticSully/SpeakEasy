using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.Speech.Recognition;
using System.Collections;

namespace SpeakEasy
{
    /*! 
     * \brief Root class of plugin inheritance.
     *
     * This class is a grandparent of all plugins.  It contains a good chunk of the reflection
     * engine that makes this system work.  This part of the engine gathers information
     * about a plugin and provides it to the main engine.
     * 
     * \author Ryan Sullivan
     * \version 0.0.1
     * \date 04-19-2012
     * \copyright All Rights Reserved.
     */
    public class PluginBase
    {
        internal string[] startCommands = { "Kinect", "Windows", "Computer" }; //!< Default, hardcoded, start commands.
        
        /// Methods to ignore during grammar building.  If any methods
        /// are added to this class that are not private, they must be
        /// added to this array.
        private string[] ignoreMethods = { "ToString", "Equals", "GetHashCode", "GetType", "getGrammarMetadata", "buildGrammar", "get_Grammars", "get_MethodParameterInfo" };
        private List<Grammar> commandGrammars;
        private Dictionary<string, ParameterInfo[]> methodParameterInfo;

        public PluginBase()
        {
            commandGrammars = new List<Grammar>();
            methodParameterInfo = new Dictionary<string, ParameterInfo[]>();
        }
        
        /// \brief List of all method grammars.
        /// 
        /// Each method in a plugin generates a unique grammar for the voice
        /// recognizer.
        public List<Grammar> Grammars
        {
            get
            {
                return commandGrammars;
            }
        }

        /// \brief Information about each method's parameters.
        /// 
        /// Each method is stored by name with an array of ParameterInfo
        /// objects, one for each parameter.
        public Dictionary<string, ParameterInfo[]> MethodParameterInfo
        {
            get
            {
                return methodParameterInfo;
            }
        }

        /// \brief Generates one grammar for each public method in a plugin.
        /// 
        /// For more information about how grammars are generated, look at \ref GrammarGeneration "Grammar Generation".
        public void buildGrammar()
        {
            // Get Subclass' Methods for use in the Grammar
            MethodInfo[] methods = this.GetType().GetMethods();

            // some helper variables
            GrammarBuilder builder;
            List<ParameterInfo> metaData;
            Dictionary<string, int> methodCounts = new Dictionary<string, int>();

            // iterate through each method to create the grammar
            foreach (MethodInfo method in methods.Where(item => !ignoreMethods.Contains(item.Name)))
            {
                // pull important names
                string methodName = method.Name;
                string className = this.GetType().Name;
                //Console.WriteLine(className);
                //see if this is an overloaded method, if so increment the uid counter.
                if (methodCounts.Keys.Contains(methodName))
                    methodCounts[methodName]++;
                else // or add in a new method
                    methodCounts.Add(methodName, 0);

                // modify method name to include uid counter.
                methodName += "-" + methodCounts[methodName];

                // get a fresh grammar builder
                builder = getBaseGrammarBuilder(method.Name);
                metaData = new List<ParameterInfo>();

                // Get the Method's parameters for processing
                foreach (ParameterInfo param in method.GetParameters())
                {
                    // if parameter is an enum
                    if (param.ParameterType.IsEnum)
                        builder.Append(new Choices(param.ParameterType.GetEnumNames())); // add its values as possible speech commands
                    else // drop into Dictionary Mode
                    {
                        FieldInfo field = this.GetType().GetField(param.Name);
                        IDictionary tempDict = (IDictionary)field.GetValue(this); // get dictionary from plugin
                        Dictionary<string, object> Dict = Engine.CastDict(tempDict).ToDictionary(entry => (string)entry.Key,
                                                                                                  entry => entry.Value);

                        builder.Append(new Choices(Dict.Keys.ToArray())); // append dictionary strings to builder as possible speech commands.
                    }

                    // add param to metadata.
                    metaData.Add(param);
                    // add in wildcard.
                    builder.AppendWildcard();
                }
                
                // see if there were any parameters, if so create metadata array
                if (metaData.Count > 0)
                    methodParameterInfo.Add(methodName, metaData.ToArray());
                else // else make null
                    methodParameterInfo.Add(methodName, null);

                // grammar name is combination of Classname + methodName to avoid conflicts
                string grammarName = className + ":" + methodName;
                Grammar methodGrammar = new Grammar(builder);

                methodGrammar.Name = grammarName;
                commandGrammars.Add(methodGrammar);
            }
        }

        /// \brief builds the base grammar
        /// 
        /// \returns the base grammar for this plugin
        private GrammarBuilder getBaseGrammarBuilder(String methodName)
        {
            GrammarBuilder builder = new GrammarBuilder { Culture = Engine.RecognizerInfo.Culture };

            builder.Append(new Choices(startCommands));
            builder.AppendWildcard();

            foreach (string word in methodName.Split('_'))
            {
                builder.Append(new Choices(word));
                builder.AppendWildcard();
            }

            return builder;
        }
    }
}

/// \page GrammarGeneration Grammar Generation
/// \section BasicGrammar Basic Grammar Generation
/// Each method generates a unique grammar based off of the method's prototype.
/// The grammar is formed by concatenating the init phrases, method name, and the
/// parameters (see below), all separated by a wildcard (ending in a wildcard). 
/// This allows some basic natural language processing to take place.  Each command
/// can be spoken with any amount of words or phrases inbetween. 
/// 
/// \par For Example ...
/// \n
/// Consider the following prototype:
/// \code 
/// public void time();
/// \endcode
/// \n
/// This will generate the grammar <b>"Kinect ... time ..."</b> where <b>"..."</b> stands for
/// a wildcard.  Strictly speaking the spoken command is <b>"Kinect time"</b>, but due
/// to the wildcards allowing for some basic NLP the phrase \"<b>Kinect</b> what <b>time</b> is it?"
/// will also be accepted by the grammar.
/// 
/// \section ParameterGrammar Parameter Grammar Generation
/// Parameters are processed into different possible sub-commands for a given method.  There are
/// two classes of parameters: <b>Enums</b> and <b>everything else</b>.
/// 
/// \par Enums
/// These are used to create simple variable commands for the same function.  Each named constant
/// gets parsed into a possible command.
/// \n
/// \n
/// Consider the following code:
/// \code
/// public enum Day {Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday};
/// 
/// public void today(Day dayInput);
/// \endcode
/// \n
/// This will generate the grammar <b>"Kinect ... today ... {Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday} ..."</b>.
/// As long as the words <b>Kinect</b>, <b>today</b>, and any constant from the enum are spoken in that order, the 
/// grammar will accept the command.  Keep in mind that any words or phrases can be spoken inbetween so \"<b>Kinect</b>
/// is <b>today</b> <b>Tuesday</b>?" would also be accepted.
/// 
/// \par Other Parameters
/// Any other kind of parameter gets to be a bit more involved.  These can be used to create
/// commands can be used to create commands that are created at startup.  These can involve file
/// names from a users computer, program names of installed programs, or anything that is not
/// necessarialy known when writing the plugin. As long as a parameter is not of
/// type <b>enum</b> the second mode will be tripped.  These parameters must reference a
/// <b>Dictionary</b> member of the Plugin.  This dictionay will be in the format:
/// \code
/// public Dictionary<string, TypeOfParameter> nameOfParameter;
/// \endcode
/// The dictionaries should be made at startup, either in the constructor of the plugin, or some sort of 
/// init function the constructor calls.  The <b>string</b> portion of the dictionary will be the
/// spoken command.  The <b>Other Type</b> will be what is passed in as the parameter if that command
/// is spoken.
/// \n
/// Consider the following:
/// \code
/// public Dictionary<string, string> musicFiles;
/// 
/// public PluginName()
/// {
///     ... code to add all music files in a folder 
///     ... lets say the first string is the file name
///     ... and the second is the file path.
/// }
/// 
/// public void play(string musicFiles)
/// {
///     ... code to start playing the file.
/// }
/// \endcode
/// \n
/// This would generate a grammar of <b>"Kinect ... play ... {any file name in dictionary} ..."</b>.  This
/// allows us to say <b>"Kinect play Code Monkey."</b> or \"<b>Kinect</b> please <b>play Code Monkey</b> for me."
/// \n
/// This works with any variable type as the second type in the Dictionary, even developer defined ones.