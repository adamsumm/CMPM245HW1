
using System;
using System.IO;
using java.util;
using java.io;
using edu.stanford.nlp.pipeline;
using Console = System.Console;

namespace HW1 {
    class Program {
        static void Main(string[] args) {
            // Text for processing
            var text = "Kosgi Santosh sent an email to Stanford University. He didn&#39;t get a reply.";
            
            // Annotation pipeline configuration
            var props = new Properties();
            props.setProperty("annotators", "tokenize, ssplit, pos, lemma, ner, parse, dcoref");
            props.setProperty("sutime.binders", "0");
            var annotation = new Annotation(text);


            var jarRoot = @"../../stanford-corenlp-3.5.0-models/";


            var curDir = Environment.CurrentDirectory;
            Directory.SetCurrentDirectory(jarRoot);
            var pipeline = new StanfordCoreNLP(props);
            Directory.SetCurrentDirectory(curDir);

            pipeline.annotate(annotation);

        }
    }
}
