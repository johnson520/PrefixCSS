using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PrefixCSS
{
	class CSSPrefixes
	{
		private static readonly Regex rxPrefixedLine = new Regex(@"^\s*-(?:ms|moz|webkit|o)-", RegexOptions.Compiled);
		private static readonly Regex rxPrefixedAtKeyframes = new Regex(@"^\s*@-(?:ms|moz|webkit|o)-keyframes", RegexOptions.Compiled);
		private static readonly Regex rxCalc = new Regex(@"\bcalc\((?<inner>.+?)\)\s*;");
		private static readonly Regex rxPercentUnits = new Regex(@"[\-\d\.]+%");

		private static readonly Regex rxPropertiesToWebkitPrefix = new Regex(@"\b(?<!\-)(?<keyword>transform|transition|animation|user-select|font-feature-settings|box-sizing)\b", RegexOptions.Compiled);
		private static readonly Regex rxPropertiesToMozPrefix = new Regex(@"\b(?<!\-)(?<keyword>transform|transition|animation|user-select|font-feature-settings|box-sizing)\b", RegexOptions.Compiled);
		private static readonly Regex rxPropertiesToMsPrefix = new Regex(@"\b(?<!\-)(?<keyword>user-select|font-feature-settings)\b", RegexOptions.Compiled);

		public static void Add(string filepath)
		{
			var fileName = Path.GetFileName(filepath);
			var lines = new List<string>(File.ReadAllLines(filepath, Encoding.UTF8));

			ClearExistingPrefixes(lines);

			var nChanges = 0;
			nChanges += AddVendorPrefix("-webkit-", lines, rxPropertiesToWebkitPrefix, fileName);
			nChanges += AddVendorPrefix("-moz-", lines, rxPropertiesToMozPrefix, fileName);
			nChanges += AddVendorPrefix("-ms-", lines, rxPropertiesToMsPrefix, fileName);

			if (nChanges == 0)
			{
				Console.WriteLine("No changes made in {0}", fileName);
			}

			var newFilePath = filepath + ".prefixed.css";
			File.WriteAllLines(newFilePath, lines, Encoding.UTF8);
			Console.WriteLine("Created {0} from {1}", newFilePath, filepath);
		}

		private static int AddVendorPrefix(string prefix, IList<string> lines, Regex rxPropertyToPrefix, string fileName)
		{
			var nChanges = 0;

			for (var i = 0; i < lines.Count; ++i)
			{

				if (prefix != "-ms-" && lines[i].StartsWith("@keyframes "))
				{
					var prefixedKeyframes = new List<string> { string.Empty, lines[i].Replace("keyframes", prefix + "keyframes") };

					//	make a copy of the keyframes block with vendor prefixes
					while (lines[++i] != "}")
					{
						prefixedKeyframes.Add(rxPropertyToPrefix.Replace(lines[i], match => prefix + match.Value));
					}
					prefixedKeyframes.Add(lines[i++]);

					for (var j = prefixedKeyframes.Count - 1; j >= 0; --j)
					{
						lines.Insert(i, prefixedKeyframes[j]);
					}
					i += prefixedKeyframes.Count;// place i just beyond our inserted block
				}

				//	this can happen if we just processed keyframes
				if (!(i < lines.Count))
					break;

				if (rxCalc.IsMatch(lines[i]))
				{
					++nChanges;

					lines[i] = rxCalc.Replace(lines[i], delegate(Match match)
					{
						var exp = match.Groups["inner"].Value;

						if (rxPercentUnits.IsMatch(exp))
						{
							Console.WriteLine("    Skipping '{0}' in {1}", match.Value, fileName);
							return match.Value;
						}

						exp = exp.Replace("px", "");
						try
						{
							var result = Convert.ToDouble(new DataTable().Compute(exp, null));
							var roundedResult = Math.Round(result, 0);

							if (result < roundedResult || result > roundedResult)
								Console.WriteLine("    '{0}' resulted in odd value {1}; rounding to {2}", match.Value, result, roundedResult);

							return string.Format("{0}px;", roundedResult);
						}
						catch (SyntaxErrorException)
						{
							Console.WriteLine("!! Could not compute '{0}' in {1}", match.Value, fileName);
							return match.Value;
						}
					});
				}

				if (rxPropertyToPrefix.IsMatch(lines[i]))
				{
					++nChanges;
					var prefixedLine = rxPropertyToPrefix.Replace(lines[i], match => prefix + match.Value);
					lines.Insert(i, prefixedLine);
					++i;	// adjust for inserted line
				}
			}

			return nChanges;
		}

		private static void ClearExistingPrefixes(IList<string> lines)
		{
			//	take care of the single-line rules
			for (var i = 0; i < lines.Count; ++i)
			{
				if (!rxPrefixedLine.IsMatch(lines[i]))
					continue;

				Console.WriteLine("Removed '{0}'", lines[i]);

				lines.RemoveAt(i);
				--i;	// adjust for removed line
			}

			for (var i = 0; i < lines.Count; ++i)
			{
				if (!rxPrefixedAtKeyframes.IsMatch(lines[i]))
					continue;

				Console.WriteLine("Removed '{0}'", lines[i]);

				var startDeletingAt = i;
				var braces = 0;

				//	find the opening brace
				while (braces == 0)
				{
					if (lines[i].Contains("{"))
						braces = 1;

					++i;
				}

				while (braces > 0)
				{
					if (lines[i].Contains("{"))
						++braces;

					if (lines[i].Contains("}"))
						--braces;

					++i;
				}

				//	i is now below the closing brace
				var linesToDelete = i - startDeletingAt;

				while (linesToDelete > 0)
				{
					lines.RemoveAt(startDeletingAt);
					--linesToDelete;
				}

				i = startDeletingAt - 1;	// adjust i for the removal and the for loop increment
			}
		}
	}
}
