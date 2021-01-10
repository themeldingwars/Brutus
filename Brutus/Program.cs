using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Brutus.Shared;
using FauFau.Formats;
using FauFau.Util;
using Checksum = Brutus.Shared.Checksum;

namespace Brutus
{
    class Program
    {
        static void Main(string[] args)
        {
            
            RootCommand cmd = new RootCommand
            {
                Name = "Brutus",
                Description = "StaticDB field and table name bruteforcer!",
            };
            
            Command generate = new Command("generate", "Generates a brutus result file from StaticDBs")
            {
                new Option<FileInfo>(new []{"--input", "-i"}, "StaticDB file or directory of dbs to generate result from")
                {
                    IsRequired = true
                },
                new Option<FileInfo>(new []{"--output", "-o"}, ()=>new FileInfo("brutus.json"), "File to output result to")
                {
                    
                }
            };
            
            
            generate.TreatUnmatchedTokensAsErrors = true;
            generate.Handler = CommandHandler.Create<FileInfo, FileInfo>(Generate);
            
            
            Command attack = new Command("attack", "Perform an attack and fill the result file with matches")
            {
                new Option<AttackType>(new []{"--type", "-a"}, "Attack type")
                {
                    IsRequired = true
                },
                new Option<FileInfo>(new []{"--target", "-t"}, "File to target")
                {
                    IsRequired = true
                },
                new Option<FileInfo>(new []{"--result", "-r"}, ()=>new FileInfo("brutus.json"), "File to output result to")
                {
                        
                }
            };

            attack.Handler = CommandHandler.Create<AttackType, FileInfo, FileInfo>(Attack);
            attack.TreatUnmatchedTokensAsErrors = true;

            
            Command export = new Command("export", "Export the best guesses from a brutus result json as a flat dictionary file. (Legacy SDBrowser fields file)")
            {
                new Option<FileInfo>(new []{"--input", "-i"}, ()=>new FileInfo("brutus.json"), "Input results json file")
                {
                        
                },
                new Option<FileInfo>(new []{"--output", "-o"}, ()=>new FileInfo("fields.txt"),"File to write the dictionary to")
                {
                    
                }
            };
            
            export.Handler = CommandHandler.Create<FileInfo, FileInfo>(Export);
            export.TreatUnmatchedTokensAsErrors = true;
            
            Command test = new Command("test", "Test a hash against strings")
            {
                new Option<string>(new[] {"--hash", "-h"}, "Hash you want to test: 0xAABBCCDD")
                {
                    IsRequired = true
                },
                new Option<string[]>(new[] {"--strings", "-s"}, "Strings to hash")
                {
                    Argument = new Argument<string[]>(),
                    IsRequired = true
                }
            };
                
            test.TreatUnmatchedTokensAsErrors = true;
            test.Handler = CommandHandler.Create<string, string[]>(Test);

            cmd.AddCommand(generate);
            cmd.AddCommand(attack);
            cmd.AddCommand(export);
            cmd.AddCommand(test);
            
            cmd.InvokeAsync(args).Wait();
        }

        static void Generate(FileInfo input, FileInfo output)
        {
            List<string> files = new List<string>();
            if (Directory.Exists(input.FullName))
            {
                files = Directory.GetFiles(input.FullName, "*.sd2", SearchOption.AllDirectories).ToList();
            }
            else if (!input.Exists)
            {
                Console.WriteLine("Input file does not exist!");
                return;
            }
            else
            {
                files.Add(input.FullName);
            }

            
            
            HashSet<string> uniqueSet = new HashSet<string>();
            List<string> uniqueStrings = new List<string>();
            
            Result result = new Result();
            
            StaticDB sdb;
            int x = 1;
            foreach (string sdbFile in files)
            {
                Console.WriteLine($"Parsing SDB file {x} of {files.Count}: {sdbFile}");
                x++;
                
                sdb = new StaticDB();
                
                try
                {
                    sdb.Read(sdbFile);
                }
                catch (Exception)
                {
                    Console.WriteLine("Parsing returned an error, maybe it's too old?");
                    continue;
                }
                
                
                foreach (StaticDB.Table sdbTable in sdb)
                {
                    // add hashes
                    AddHashHashToResult(sdbTable.Id);
                    for (int col = 0; col < sdbTable.Columns.Count; col++)
                    {
                        StaticDB.Column sdbColumn = sdbTable.Columns[col];
                        AddHashHashToResult(sdbColumn.Id);
                        
                        // check strings
                        if (sdbColumn.Type == StaticDB.DBType.String)
                        {
                            foreach (StaticDB.Row row in sdbTable)
                            {
                                string str = ((string) row.Fields[col]);
                                if (str != null)
                                {

                                    string w = str.Trim();
                                    string lw = w.ToLower();
                                    string uw = w.ToUpper();

                                    if (!uniqueSet.Contains(w)) { uniqueSet.Add(w); uniqueStrings.Add(w); }
                                    if (!uniqueSet.Contains(lw)) { uniqueSet.Add(lw); uniqueStrings.Add(lw); }
                                    if (!uniqueSet.Contains(uw)) { uniqueSet.Add(uw); uniqueStrings.Add(uw); }
                    
                                    string[] words2 = w.Split(new[] {'<', '>', '=', '"', '\\', '/', '.', '-', '_', '\0'});

                                    foreach (string word2 in words2)
                                    {
                                        w = word2.Trim();
                                        lw = w.ToLower();
                                        uw = w.ToUpper();

                                        if (!uniqueSet.Contains(w)) { uniqueSet.Add(w); uniqueStrings.Add(w); }
                                        if (!uniqueSet.Contains(lw)) { uniqueSet.Add(lw); uniqueStrings.Add(lw); }
                                        if (!uniqueSet.Contains(uw)) { uniqueSet.Add(uw); uniqueStrings.Add(uw); }

                                    }
                                }
                            }
                        }
                    }
                }
                sdb = null;
                GC.Collect();
            }

            Console.WriteLine($"Found a total of {uniqueStrings.Count} unique strings!");
            Console.WriteLine($"Performing dictionary attack...");

            foreach (string str in uniqueStrings)
            {
                if (result.TryGetValue(Checksum.FFnv32(str), out Match match))
                {
                    if (!match.Matches.Contains(str))
                    {
                        match.Matches.Add(str);
                        if (match.BestGuess == null)
                        {
                            match.BestGuess = str;
                        }
                    }
                }
            }
            
            Console.WriteLine($"Saving results to: {output.FullName}");
            result.Save(output.FullName);
            
            void AddHashHashToResult(uint hash)
            {
                if (!result.ContainsKey(hash))
                {
                    result.Add(hash, new Match());
                }
            }
        }

        static void Attack(AttackType type, FileInfo target, FileInfo result)
        {
            //resultFile = new FileInfo("brutus.json");

            if (!File.Exists(result.Name))
            {
                Console.WriteLine($"Result file {result.FullName} does not exist, you can generate a new one with 'brutus generate'");
                return;
            }

            Result brutusResult;
            try
            {
                //Console.WriteLine(resultFile.FullName);
                brutusResult = Result.Load(result.FullName);
            }
            catch (Exception)
            {
                Console.WriteLine("Was that really a brutus result file?");
                return;
            }

            string[] targets = new string[0];
            bool ok = target.Exists;
            if (ok)
            {
                targets = new[] {target.FullName};
            }
            else if (target.FullName.Contains('|'))
            {
                string[] split = target.FullName.Split('|');
                if (split.Length == 2 && Directory.Exists(split[0]))
                {
                    targets = Directory.GetFiles(split[0], split[1], SearchOption.AllDirectories);

                    if (targets.Length == 0)
                    {
                        Console.WriteLine($"No targets match pattern :<");
                        return;;
                    }
                    ok = true;
                }
            }

            if (!ok)
            {
                Console.WriteLine($"Target file {target.FullName} does not exist. You can match multiple files recursively with --target path/to/basedir|*pattern.ext");
                return;
            }

            int matches = 0;
            switch (type)
            {
                case AttackType.Dictionary:
                    DictionaryAttack(ref brutusResult, targets, out matches);
                    break;
                case AttackType.Binary:
                    BinaryAttack(ref brutusResult, targets, out matches);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            
            if(matches > 0)
            {
                Console.WriteLine($"Got {matches} new matches, updating result file! :D");
                brutusResult.Save(result.FullName);
            }
            else
            {
                Console.WriteLine("No more matches found :<");
            }

            int has = 0;
            foreach (Match match in brutusResult.Values)
            {
                if (match.Matches.Count > 0) has++;
            }
            
            Console.WriteLine($"Result file now contains matches for {has} of {brutusResult.Count} hashes.");
            Console.WriteLine((100d/brutusResult.Count)*has + "% coverage of all hashes! :>");
        }
        
        static bool DictionaryAttack(ref Result result, string[] targets, out int matches)
        {
            HashSet<string> uniqueSet = new HashSet<string>();
            List<string> uniqueStrings = new List<string>();
            
            foreach (string file in targets)
            {
                Console.WriteLine($"Looking for unique strings in {file}");

                string[] words = File.ReadAllText(file).Split(new []{' ', '\r', '\n', '\t'});

                foreach (string word in words)
                {
                    string w = word.Trim();
                    string lw = w.ToLower();
                    string uw = w.ToUpper();

                    if (!uniqueSet.Contains(w)) { uniqueSet.Add(w); uniqueStrings.Add(w); }
                    if (!uniqueSet.Contains(lw)) { uniqueSet.Add(lw); uniqueStrings.Add(lw); }
                    if (!uniqueSet.Contains(uw)) { uniqueSet.Add(uw); uniqueStrings.Add(uw); }
                    
                    string[] words2 = w.Split(new[] {'<', '>', '=', '"', '\\', '/', '.', '-', '_', '\0'});

                    foreach (string word2 in words2)
                    {
                        w = word2.Trim();
                        lw = w.ToLower();
                        uw = w.ToUpper();

                        if (!uniqueSet.Contains(w)) { uniqueSet.Add(w); uniqueStrings.Add(w); }
                        if (!uniqueSet.Contains(lw)) { uniqueSet.Add(lw); uniqueStrings.Add(lw); }
                        if (!uniqueSet.Contains(uw)) { uniqueSet.Add(uw); uniqueStrings.Add(uw); }

                    }
                }
            }

            matches = 0;
            if (uniqueStrings.Count > 0)
            {
                Console.WriteLine($"Performing attack with the {uniqueStrings.Count} unique strings found :>");

                foreach (string str in uniqueStrings)
                {
                    if (result.TryGetValue(Checksum.FFnv32(str), out Match match))
                    {
                        if (!match.Matches.Contains(str))
                        {
                            match.Matches.Add(str);
                            if (match.BestGuess == null)
                            {
                                match.BestGuess = str;
                            }

                            Console.WriteLine($"Got match! 0x{Checksum.FFnv32(str).ToString("x8")} : {str}");
                            matches++;
                        }
                    }
                }
                return true;
            }
            return false;
        }
        
        static bool BinaryAttack(ref Result result, string[] targets, out int matches)
        {
            HashSet<string> uniqueSet = new HashSet<string>();
            List<string> uniqueStrings = new List<string>();
            
            foreach (string file in targets)
            {
                Console.WriteLine($"Looking for unique strings in {file}");

                Span<byte> data = File.ReadAllBytes(file);


                int start = 0;
                int next = 0;
                bool found = false;
                int minLen = 3;
                
                while (next < data.Length)
                {
                    bool valid = data[next] > 32 && data[next] < 127;
                    if (!found)
                    {
                        // range of printable ascii strings
                        if (valid)
                        {
                            start = next;
                            found = true;
                        }
                    }
                    else
                    {
                        if (!valid)
                        {
                            found = false;
                            if (next - start > minLen)
                            {
                                string str =  Encoding.UTF8.GetString(data.Slice(start, next - start));
                                string w = str.Trim();
                                string lw = w.ToLower();
                                string uw = w.ToUpper();

                                if (!uniqueSet.Contains(w)) { uniqueSet.Add(w); uniqueStrings.Add(w); }
                                if (!uniqueSet.Contains(lw)) { uniqueSet.Add(lw); uniqueStrings.Add(lw); }
                                if (!uniqueSet.Contains(uw)) { uniqueSet.Add(uw); uniqueStrings.Add(uw); }
                                
                                string[] words2 = str.Split(new[] {'<', '>', '=', '"', '\\', '/', '.', '-', '_', '\0'});

                                foreach (string word2 in words2)
                                {
                                    w = word2.Trim();
                                    lw = w.ToLower();
                                    uw = w.ToUpper();

                                    if (!uniqueSet.Contains(w)) { uniqueSet.Add(w); uniqueStrings.Add(w); }
                                    if (!uniqueSet.Contains(lw)) { uniqueSet.Add(lw); uniqueStrings.Add(lw); }
                                    if (!uniqueSet.Contains(uw)) { uniqueSet.Add(uw); uniqueStrings.Add(uw); }
                                }
                            }
                        }
                    }
                    next++;
                }
            }

            matches = 0;
            if (uniqueStrings.Count > 0)
            {
                Console.WriteLine($"Performing attack with the {uniqueStrings.Count} unique strings found :>");

                foreach (string str in uniqueStrings)
                {
                    if (result.TryGetValue(Checksum.FFnv32(str), out Match match))
                    {
                        if (!match.Matches.Contains(str))
                        {
                            match.Matches.Add(str);
                            if (match.BestGuess == null)
                            {
                                match.BestGuess = str;
                            }

                            Console.WriteLine($"Got match! 0x{Checksum.FFnv32(str).ToString("x8")} : {str}");
                            matches++;
                        }
                    }
                }
                return true;
            }
            return false;
        }


        static void Export(FileInfo input, FileInfo output)
        {
            if (!File.Exists(input.Name))
            {
                Console.WriteLine($"Result file {input.FullName} does not exist, you can generate a new one with 'brutus generate'");
                return;
            }

            Result brutusResult;
            try
            {
                //Console.WriteLine(resultFile.FullName);
                brutusResult = Result.Load(input.FullName);
            }
            catch (Exception)
            {
                Console.WriteLine("Was that really a brutus result file?");
                return;
            }
            
            
            Console.WriteLine($"Dumping best guesses to {output.FullName}");
            List<string> bestGuesses = new List<string>();
            foreach (Match match in brutusResult.Values)
            {
                if (match.BestGuess != null)
                {
                    bestGuesses.Add(match.BestGuess);
                }
            }

            File.WriteAllLines(output.FullName, bestGuesses);

        }
        static void Test(string hash, string[] strings)
        {
            if(hash.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
            {
                uint fnv;
                bool match = false;
                if (uint.TryParse(hash.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out fnv))
                {
                    foreach (string str in strings)
                    {
                        uint strHash = Checksum.FFnv32(str);
                        if (strHash == fnv)
                        {
                            Console.WriteLine($"Match! :> {hash} : {str}");
                        }
                    }

                    if (!match)
                    {
                        Console.WriteLine("No matches :<");
                    }
                    return;
                }
            }
            Console.WriteLine("Badly formatted hash, please us this format: 0xAABBCCDD");
        }
        
        public enum AttackType
        {
            Dictionary,
            Binary
        }
        
        public byte[][] ReadDict()
        {
            System.IO.StreamReader file = new System.IO.StreamReader(@"\words.txt");
            string line;
            while ((line = file.ReadLine()) != null)
            {
                System.Console.WriteLine(line);
                //counter++;
            }

            return new byte[0][];
        }
    }
}