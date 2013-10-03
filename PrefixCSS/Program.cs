namespace PrefixCSS
{
	class Program
	{
		static void Main(string[] args)
		{
			foreach (var filename in args)
				CSSPrefixes.Add(System.IO.Path.GetFullPath(filename));
		}
	}
}
