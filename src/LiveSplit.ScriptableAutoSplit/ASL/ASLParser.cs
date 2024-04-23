﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Irony.Parsing;

using LiveSplit.ComponentUtil;

namespace LiveSplit.ASL
{
    public class ASLParser
    {
        public static ASLScript Parse(string code)
        {
            var grammar = new ASLGrammar();
            var parser = new Parser(grammar);
            var tree = parser.Parse(code);

            if (tree.HasErrors())
            {
                var error_msg = new StringBuilder("ASL parse error(s):");
                foreach (var msg in parser.Context.CurrentParseTree.ParserMessages)
                {
                    var loc = msg.Location;
                    error_msg.Append($"\nat Line {loc.Line + 1}, Col {loc.Column + 1}: {msg.Message}");
                }

                throw new Exception(error_msg.ToString());
            }

            var root_childs = tree.Root.ChildNodes;
            var methods_node = root_childs.First(x => x.Term.Name == "methodList");
            var states_node = root_childs.First(x => x.Term.Name == "stateList");

            var states = new Dictionary<string, List<ASLState>>();

            foreach (var state_node in states_node.ChildNodes)
            {
                var process_name = (string)state_node.ChildNodes[2].Token.Value;
                var version =
                    state_node.ChildNodes[3].ChildNodes.Skip(1).Select(x => (string)x.Token.Value).FirstOrDefault() ??
                    string.Empty;
                var value_definition_nodes = state_node.ChildNodes[6].ChildNodes;

                var state = new ASLState();

                foreach (var value_definition_node in value_definition_nodes.Where(x => x.ChildNodes.Count > 0))
                {
                    var child_nodes = value_definition_node.ChildNodes;
                    var type = (string)child_nodes[0].Token.Value;
                    var identifier = (string)child_nodes[1].Token.Value;
                    var module =
                        child_nodes[3].ChildNodes.Take(1).Select(x => (string)x.Token.Value).FirstOrDefault() ??
                        string.Empty;
                    var module_base = child_nodes[4].ChildNodes.Select(x => (int)x.Token.Value).First();
                    var offsets = child_nodes[4].ChildNodes.Skip(1).Select(x => (int)x.Token.Value).ToArray();
                    var value_definition = new ASLValueDefinition()
                    {
                        Identifier = identifier,
                        Type = type,
                        Pointer = new DeepPointer(module, module_base, offsets)
                    };
                    state.ValueDefinitions.Add(value_definition);
                }

                state.GameVersion = version;
                if (!states.ContainsKey(process_name))
                {
                    states.Add(process_name, new List<ASLState>());
                }

                states[process_name].Add(state);
            }

            var methods = new ASLScript.Methods();

            foreach (var method in methods_node.ChildNodes[0].ChildNodes)
            {
                var body = (string)method.ChildNodes[2].Token.Value;
                var method_name = (string)method.ChildNodes[0].Token.Value;
                var line = method.ChildNodes[2].Token.Location.Line + 1;
                var script = new ASLMethod(body, method_name, line)
                {
                    ScriptMethods = methods
                };
                switch (method_name)
                {
                    case "init": methods.init = script; break;
                    case "exit": methods.exit = script; break;
                    case "update": methods.update = script; break;
                    case "start": methods.start = script; break;
                    case "split": methods.split = script; break;
                    case "isLoading": methods.isLoading = script; break;
                    case "gameTime": methods.gameTime = script; break;
                    case "reset": methods.reset = script; break;
                    case "startup": methods.startup = script; break;
                    case "shutdown": methods.shutdown = script; break;
                    case "onStart": methods.onStart = script; break;
                    case "onSplit": methods.onSplit = script; break;
                    case "onReset": methods.onReset = script; break;
                }
            }

            return new ASLScript(methods, states);
        }
    }
}
