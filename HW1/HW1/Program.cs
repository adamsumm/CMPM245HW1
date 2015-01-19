
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
//using java.util;
//using java.io;

using edu.stanford.nlp.pipeline;
using edu.stanford.nlp.ling;
using edu.stanford.nlp.semgraph;
using edu.stanford.nlp.trees;
using edu.stanford.nlp.dcoref;
using Console = System.Console;

namespace HW1 {
    class Program {
        public static Tuple<List<int>, Dictionary<string, int>, Dictionary<string, int>> ProcessDocument(string filename) {
            var serializedOutput = filename.Substring(0, filename.LastIndexOf('.'));
            serializedOutput += ".serialized";

            Annotation annotation = null;
            if (File.Exists(serializedOutput)) {
                java.io.FileInputStream fis = new java.io.FileInputStream(serializedOutput);
                AnnotationSerializer serializer = new CustomAnnotationSerializer(false, false);
                var pair = serializer.read(fis);

                annotation = (Annotation)pair.first();
                java.io.FileOutputStream fos = new java.io.FileOutputStream(filename.Substring(0, filename.LastIndexOf('.'))+".xml");
			    XMLOutputter.xmlPrint(annotation, fos);
            }
            else {
                // Text for processing
                var text = System.IO.File.ReadAllText(filename);//"I went or a run. Then I went to work. I had a good lunch meeting with a friend name John Jr. The commute home was pretty good.";

                // Annotation pipeline configuration
                var props = new java.util.Properties();
                props.setProperty("annotators", "tokenize, ssplit, pos, lemma, ner, parse, dcoref");
                props.setProperty("sutime.binders", "0");
                annotation = new Annotation(text);


                var jarRoot = @"../../stanford-corenlp-3.5.0-models/";


                var curDir = Environment.CurrentDirectory;
                Directory.SetCurrentDirectory(jarRoot);
                var pipeline = new StanfordCoreNLP(props);
                Directory.SetCurrentDirectory(curDir);

                pipeline.annotate(annotation);
                CustomAnnotationSerializer serializer = new CustomAnnotationSerializer(false, false);
                java.io.FileOutputStream fos = new java.io.FileOutputStream(serializedOutput);
                serializer.write(annotation, fos);
                fos.close();
            }

            var sentences = annotation.get(typeof(CoreAnnotations.SentencesAnnotation));
            Dictionary<string, int> partsOfSpeech = new Dictionary<string, int>();
            Dictionary<string, int> namedEntities = new Dictionary<string, int>();

            
            List<string> depChainStrings = new List<string>();
            foreach (Annotation sentence in sentences as java.util.ArrayList) {
                foreach (CoreLabel token in (sentence.get(typeof(CoreAnnotations.TokensAnnotation)) as java.util.ArrayList)) {
                    string word = (string)token.get(typeof(CoreAnnotations.TextAnnotation));
                    var indexObj = ((java.lang.Integer)token.get(typeof(CoreAnnotations.IndexAnnotation)));
                    var index = 0;
                    if (indexObj != null) {
                        index = indexObj.intValue();
                    }
                    string pos = (string)token.get(typeof(CoreAnnotations.PartOfSpeechAnnotation));
                    string ner = (string)token.get(typeof(CoreAnnotations.NamedEntityTagAnnotation));
                    if (!partsOfSpeech.ContainsKey(pos)) {
                        partsOfSpeech[pos] = 1;
                    }
                    else {
                        partsOfSpeech[pos]++;
                    }
                    if (!namedEntities.ContainsKey(ner)) {
                        namedEntities[ner] = 1;
                    }
                    else {
                        namedEntities[ner]++;
                    }
                    //Console.WriteLine("|" + word + "-" + index + "| -- |" + pos + "| -- |" + ner + "|");

                }
                SemanticGraph deps = (SemanticGraph)sentence.get(typeof(SemanticGraphCoreAnnotations.CollapsedCCProcessedDependenciesAnnotation));
                Dictionary<int, Tuple<string, List<string>>> depChain = new Dictionary<int, Tuple<string, List<string>>>();
                foreach (SemanticGraphEdge edge in deps.edgeListSorted().toArray()) {
                    var gov = edge.getGovernor();
                    var dep = edge.getDependent();
                    string pos = (string) gov.get(typeof(CoreAnnotations.PartOfSpeechAnnotation)) ;
                    if (pos.Contains("VB") && ( edge.getRelation().getShortName() == "nsubj" || edge.getRelation().getShortName() == "dobj" || edge.getRelation().getShortName() == "iobj")) {
                        if (!depChain.ContainsKey(gov.index())) {
                            depChain[gov.index()] = new Tuple<string, List<string>>(gov.word(), new List<string>());
                        }
                        depChain[gov.index()].Item2.Add(dep.word());
                    }
                }
                foreach (var item in depChain.OrderBy(i => i.Key)) {
                    depChainStrings.Add(item.Value.Item1 + "(" + string.Join(",", item.Value.Item2.ToArray()) + ")");
                }
            }
            var depChainName = filename.Substring(0, filename.LastIndexOf('.'));
            depChainName += ".chain";
            System.IO.File.Delete(depChainName);
            System.IO.File.AppendAllLines(depChainName, depChainStrings);

            java.util.HashMap graph = (java.util.HashMap)annotation.get(typeof(CorefCoreAnnotations.CorefChainAnnotation));

            // Output the chains
            List<int> mentions = new List<int>();
            foreach (CorefChain chain in graph.values().toArray()) {
          //      Console.WriteLine(chain.getRepresentativeMention());
                mentions.Add(chain.getMentionsInTextualOrder().toArray().Length);
                /*
                foreach (CorefChain.CorefMention mention in chain.getMentionsInTextualOrder().toArray()) {

                    Console.Write("\t{0:G}, {1:G}\n", mention.position, mention.mentionSpan);
                    Console.Write(
                        "\t\tAnimacy: {0:G}, Gender:{1:G}, Number:{2:G}, Type:{3:G}\n",
                        mention.animacy, mention.gender, mention.number, mention.mentionType
                    );
                }
                 * */
            }

          //  var stats = GetSummaryStatistics(mentions);
            return new Tuple<List<int>, Dictionary<string, int>, Dictionary<string, int>>(mentions, partsOfSpeech, namedEntities);
        }

        public static List<int> ProcessXML(string filename) {

            XDocument summaryDoc = XDocument.Load(filename);
            List<int> corefChain = new List<int>();
            foreach (XElement doc in summaryDoc.Root.Elements()) {
                foreach (XElement outer in doc.Elements()) {
                    foreach (XElement coref in outer.Elements()) {
                        int mentions = 0;
                        foreach (XElement mention in coref.Elements()) {
                            mentions++;

                        }
                        corefChain.Add(mentions);
                    }
                }
            }
            return corefChain;
        }
        static List<string> ProcessGenre(string genreName,IEnumerable<string> files) {
            List<int> mentions = new List<int>();
            List<int> chains = new List<int>();
            Dictionary<string, int> pos = new Dictionary<string, int>();
            Dictionary<string, int> ner = new Dictionary<string, int>();
            int posTotal = 0;
            int nerTotal = 0;
            foreach (var file in files) {
                if (file.Contains(".coreference")) {
                    var output = ProcessXML(file);
                    var stats = GetSummaryStatistics(output);
                    chains.AddRange(output);
                    mentions.Add(stats.Item5);
                }
                else {
                    var output = ProcessDocument(file);
                    var stats = GetSummaryStatistics(output.Item1);
                    chains.AddRange(output.Item1);
                    mentions.Add(stats.Item5);

                    foreach (var kv in output.Item2) {
                        posTotal += kv.Value;
                        if (!pos.ContainsKey(kv.Key)) {
                            pos[kv.Key] = kv.Value;
                        }
                        else {
                            pos[kv.Key] += kv.Value;
                        }
                    }
                    foreach (var kv in output.Item3) {
                        nerTotal += kv.Value;
                        if (!ner.ContainsKey(kv.Key)) {
                            ner[kv.Key] = kv.Value;
                        }
                        else {
                            ner[kv.Key] += kv.Value;
                        }
                    }
                }
            }

            var chainStats = GetSummaryStatistics(chains);
            var mentionStats = GetSummaryStatistics(mentions);
            List<string> outstrs = new List<string>();
            foreach (var kv in pos) {
                outstrs.Add(genreName + " " + kv.Key + " " + kv.Value + " " + ((float)kv.Value) / ((float)posTotal));
            }
            outstrs.Add("\n");
            foreach (var kv in ner) {
                outstrs.Add(genreName + " " + kv.Key + " " + kv.Value + " " + ((float)kv.Value) / ((float)nerTotal));
            }
            outstrs.Add(genreName + " " + chainStats);
            outstrs.Add("\n");
            outstrs.Add(genreName + " " + mentionStats);
            outstrs.Add("\n");
            outstrs.Add("\n");
            return outstrs;
        }
        static Tuple<int, int, float, float,int> GetSummaryStatistics(List<int> list) {
            List<int> temp = new List<int>(list);
            temp.Sort();
            int total = 0;
            foreach (var val in temp) {
                total += val;
            }
            float median = temp[temp.Count / 2];
            if (temp.Count % 2 == 0) {
                median = (((float)temp[temp.Count / 2-1]) + ((float)temp[temp.Count / 2])) * 0.5f;
            }
            return new Tuple<int, int, float, float,int>(temp[0], temp[temp.Count - 1], median, ((float)total) / ((float)temp.Count),total);
            
        }
        static void Main(string[] args) {

            string outputFile = "summaryMod.txt";
            System.IO.File.Delete(outputFile);
            List<string> output = null;
            string[] genre1 = new string[] { "01.coreference", "04.txt", "09.txt", "11.txt" };
            output = ProcessGenre("genre1", genre1);
            System.IO.File.AppendAllLines(outputFile, output);
            string[] genre2 = new string[] { "02.txt", "05.txt", "07.txt" };
            output = ProcessGenre("genre2", genre2);
            System.IO.File.AppendAllLines(outputFile, output);
            string[] genre3 = new string[] { "03.txt", "08.txt" };
            output = ProcessGenre("genre3", genre3);
            System.IO.File.AppendAllLines(outputFile, output);
            string[] genre4 = new string[] { "06.txt", "10.txt", "12.txt"  };
            output = ProcessGenre("genre4", genre4);
            System.IO.File.AppendAllLines(outputFile, output);


            /*
            var filename = "01.txt";
            var serializedOutput = filename.Substring(0, filename.LastIndexOf('.'));
            serializedOutput += ".serialized";

            Annotation annotation = null;
            if (File.Exists(serializedOutput)) {
                java.io.FileInputStream fis = new java.io.FileInputStream(serializedOutput);
		        AnnotationSerializer serializer = new CustomAnnotationSerializer(false, false);
			    var pair = serializer.read(fis);

			    annotation = (Annotation) pair.first();
            }
            else{
                // Text for processing
                var text = System.IO.File.ReadAllText(filename);//"I went or a run. Then I went to work. I had a good lunch meeting with a friend name John Jr. The commute home was pretty good.";

                // Annotation pipeline configuration
                var props = new java.util.Properties();
                props.setProperty("annotators", "tokenize, ssplit, pos, lemma, ner, parse, dcoref");
                props.setProperty("sutime.binders", "0");
                annotation = new Annotation(text);


                var jarRoot = @"../../stanford-corenlp-3.5.0-models/";


                var curDir = Environment.CurrentDirectory;
                Directory.SetCurrentDirectory(jarRoot);
                var pipeline = new StanfordCoreNLP(props);
                Directory.SetCurrentDirectory(curDir);

                pipeline.annotate(annotation);
                CustomAnnotationSerializer serializer = new CustomAnnotationSerializer(false, false);
                java.io.FileOutputStream fos = new java.io.FileOutputStream(serializedOutput);
                serializer.write(annotation, fos);
                fos.close();
            }



            var sentences = annotation.get(typeof(CoreAnnotations.SentencesAnnotation));
            if (sentences == null) {
                return;
            }
            foreach (Annotation sentence in sentences as java.util.ArrayList) {
                foreach (CoreLabel token in (sentence.get(typeof(CoreAnnotations.TokensAnnotation)) as java.util.ArrayList)) {
                        string word= (string) token.get(typeof(CoreAnnotations.TextAnnotation));
                        var indexObj = ((java.lang.Integer)token.get(typeof(CoreAnnotations.IndexAnnotation)));
                        var index = 0;    
                        if (indexObj != null) {
                            index = indexObj.intValue();
                        }
                        string pos = (string)token.get(typeof(CoreAnnotations.PartOfSpeechAnnotation));
                        string ner = (string)token.get(typeof(CoreAnnotations.NamedEntityTagAnnotation));
                        Console.WriteLine( "|"+word + "-" + index + "| -- |" + pos+ "| -- |" +ner + "|");
                        
                }
                SemanticGraph deps = (SemanticGraph) sentence.get(typeof(SemanticGraphCoreAnnotations.CollapsedCCProcessedDependenciesAnnotation));
                Dictionary<int, Tuple<string, List<string>>> depChain = new Dictionary<int, Tuple<string, List<string>>>();
                foreach (SemanticGraphEdge edge in deps.edgeListSorted().toArray()) {
                    var gov = edge.getGovernor();
                    var dep = edge.getDependent();
                    if (edge.getRelation().getShortName() == "nsubj" || edge.getRelation().getShortName() == "dobj" || edge.getRelation().getShortName() == "iobj") {
                        if (!depChain.ContainsKey(gov.index())) {
                            depChain[gov.index()] = new Tuple<string, List<string>>(gov.word(), new List<string>());
                        }
                        depChain[gov.index()].Item2.Add(dep.word());
                    }
                    Console.WriteLine(edge.getRelation() + "(" + gov.word() + "-" + gov.index() + "," + dep.word() + "-" + dep.index() +")");
                }
                foreach (var item in depChain.OrderBy(i => i.Key)) {
                    Console.WriteLine(item.Value.Item1 + "(" + string.Join(",", item.Value.Item2.ToArray()) +")" );
                }
            }

		    java.util.HashMap graph = (java.util.HashMap) annotation.get(typeof( CorefCoreAnnotations.CorefChainAnnotation));
		
		    // Output the chains
            List<int> mentions = new List<int>();
		    foreach (CorefChain chain in graph.values().toArray()) {
			    Console.WriteLine(chain.getRepresentativeMention());
                mentions.Add(chain.getMentionsInTextualOrder().toArray().Length);
                foreach (CorefChain.CorefMention mention  in chain.getMentionsInTextualOrder().toArray()) {
                    
                    Console.Write("\t{0:G}, {1:G}\n", mention.position, mention.mentionSpan);
                    Console.Write(
                        "\t\tAnimacy: {0:G}, Gender:{1:G}, Number:{2:G}, Type:{3:G}\n", 
					    mention.animacy, mention.gender, mention.number, mention.mentionType
				    );
			    }
		    }
           
            Console.WriteLine(GetSummaryStatistics(mentions));
            
             * */

            Console.WriteLine("ALL DONE");
            Console.Read();
        }
    }
}
