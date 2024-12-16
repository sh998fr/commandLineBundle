using System.CommandLine;
using System.Text.RegularExpressions;

var LanguageExtensions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
{
    { "java", new List<string> { ".java" } },
    { "c#", new List<string> { ".cs" } },
    { "python", new List<string> { ".py" } },
    { "javascript", new List<string> { ".js" } },
    { "c++", new List<string> { ".cpp", ".cc" } },
    { "c", new List<string> { ".c", ".h" } },
    { "html", new List<string> { ".html" } },
    { "css", new List<string> { ".css" } },
    { "typescript", new List<string> { ".ts" } },
    { "go", new List<string> { ".go" } },
    { "ruby", new List<string> { ".rb" } },
    { "php", new List<string> { ".php" } },
    { "all", new List<string>() } // Placeholder for "all"
};

var excludedPatterns = new List<string>
{
    "*.Designer.cs",
    "Migrations\\*.cs",
    ".angular\\cache\\.*" // מתאים לכל רמות התיקיות
};

bool MatchesPattern(string filePath, string pattern)
{
    var regexPattern = "^" + Regex.Escape(pattern)
        .Replace("\\*", ".*")  // כוכבית אחת - כל שם קובץ או תיקיה
        .Replace("\\?", ".")    // סימן שאלה - תו יחיד
        .Replace("\\.\\*\\.\\*", ".*")  // כוכבית כפולה - כל רמות התיקיות
        + "$";

    return Regex.IsMatch(filePath, regexPattern, RegexOptions.IgnoreCase);
}

var sourceOption = new Option<DirectoryInfo>(
    aliases: new[] { "--source", "-s" },
    getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()),
    description: "The source directory to search for files."
);

sourceOption.AddValidator(option =>
{
    if (!Directory.Exists(option.GetValueOrDefault<DirectoryInfo>()?.FullName))
    {
        throw new ArgumentException("Source directory does not exist.");
    }
});

var bundleOption = new Option<FileInfo>(
    aliases: new[] { "--output", "-o" },
    description: "The file path and name for the bundled output file."
);

bundleOption.AddValidator(option =>
{
    var output = option.GetValueOrDefault<FileInfo>();
    if (output == null || string.IsNullOrWhiteSpace(output.FullName))
    {
        throw new ArgumentException("Output file must be specified.");
    }
});

var bundleOptionLanguage = new Option<string[]>(
    aliases: new[] { "--language", "-l" },
    description: "The programming languages to include (e.g., java, c#, python). Use 'all' to include all supported languages."
);

bundleOptionLanguage.AddValidator(option =>
{
    var languages = option.GetValueOrDefault<string[]>();
    if (languages == null || !languages.Any())
    {
        throw new ArgumentException("At least one language must be specified.");
    }

    foreach (var language in languages)
    {
        if (!LanguageExtensions.ContainsKey(language) && !language.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unsupported language: {language}.");
        }
    }

});

var noteSourceOption = new Option<bool>(
    aliases: new[] { "--note", "-n" },
    description: "Write the source code as a note",
    getDefaultValue: () => false
);


var sortOption = new Option<string>(
    aliases: new[] { "--sort", "-t" },
    description: "Sort files by 'name' (default) or 'type'",
    getDefaultValue: () => "name"
);



sortOption.AddValidator(option =>
{
    var sortValue = option.GetValueOrDefault<string>();
    if (!sortValue.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sortValue.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Sort option must be either 'name' or 'type'.");
    }
});


var removeEmptyLinesOption = new Option<bool>(
    aliases: new[] { "--remove-empty-lines", "-r" },
    description: "Remove empty lines from source code.",
    getDefaultValue: () => false
);

var authorOption = new Option<string>(
    aliases: new[] { "--author", "-a" },
    description: "Specify the author's name to be added as a header comment in the bundle file."
);

var bundleCommand = new Command("bundle", "Bundle code files to a single file")
{
    sourceOption,
    bundleOption,
    bundleOptionLanguage,
    noteSourceOption,
    sortOption,
    removeEmptyLinesOption, 
    authorOption
};

var createRspCommand = new Command("create-rsp", "create response file accroding the answers that the user provide");

bool IsExcluded(string filePath, HashSet<string> excludedDirectories, List<string> excludedPatterns)
{
    var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

    // בדיקת אם הקובץ בתיקיה שצריך להחריג
    if (excludedDirectories.Any(excluded => directory.Contains(excluded, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    // בדיקת אם הקובץ תואם לתבניות החרגה
    return excludedPatterns.Any(pattern => MatchesPattern(filePath, pattern));
}

HashSet<string> BuildExtensions(string[] languages)
{
    var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (languages.Contains("all", StringComparer.OrdinalIgnoreCase))
    {
        extensions = LanguageExtensions
            .Where(kvp => kvp.Key != "all")
            .SelectMany(kvp => kvp.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    else
    {
        foreach (var language in languages)
        {
            if (LanguageExtensions.TryGetValue(language, out var exts))
            {
                foreach (var ext in exts)
                {
                    extensions.Add(ext);
                }
            }
            else
            {
                Console.WriteLine($"Warning: Language '{language}' is not recognized.");
            }
        }
    }
    return extensions;
}
bundleCommand.SetHandler((DirectoryInfo source, FileInfo output, string[] languages,bool noteSource,string sortOption, bool removeEmptyLines, string? author) =>
{
    try
    {
        var extensions = BuildExtensions(languages);
        if (!extensions.Any())
        {
            Console.WriteLine("No valid extensions were identified. Exiting.");
            return;
        }

        Console.WriteLine($"Using extensions: {string.Join(", ", extensions)}");

        using var writer = new StreamWriter(output.FullName);

        // הוספת כותרת עם שם היוצר
        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"// Bundle created by: {author}");
            writer.WriteLine($"// Creation date: {DateTime.Now}");
            writer.WriteLine();
        }

        var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "node_modules", "bin", "obj", ".git" , ".angular\\cache" , "Migrations", "debug" };

        var files = Directory.GetFiles(source.FullName, "*.*", SearchOption.AllDirectories)
    .Where(file =>
        !IsExcluded(file, excludedDirectories, excludedPatterns) && // סינון קבצים לא רצויים
        extensions.Contains(Path.GetExtension(file))) // סינון לפי סיומות
    .ToList();

        if (sortOption.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            files = files.OrderBy(file => Path.GetExtension(file)).ThenBy(file => Path.GetFileName(file)).ToList();
        }
        else
        {
            files = files.OrderBy(file => Path.GetFileName(file)).ToList();
        }


        if (!files.Any())
        {
            Console.WriteLine("No matching files found.");
            return;
        }

        foreach (var file in files)
        {
            Console.WriteLine($"Adding file: {file}");

            if (noteSource)
            {
                // לחשב נתיב יחסי
                string relativePath = Path.GetRelativePath(source.FullName, file);

                // כתוב הערה עם מקור הקובץ
                writer.WriteLine($"// מקור הקוד: {relativePath}");
                writer.WriteLine($"// מקור מוחלט: {file}");
                writer.WriteLine();
            }

            using var reader = new StreamReader(file);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {

                // מחיקת שורות ריקות אם נדרש
                if (removeEmptyLines && string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                writer.WriteLine(line);
            }
        }


        Console.WriteLine($"Bundled files written to: {output.FullName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}, sourceOption, bundleOption, bundleOptionLanguage,noteSourceOption,sortOption, removeEmptyLinesOption, authorOption);

createRspCommand.SetHandler(() =>
{
    Console.WriteLine("Enter the path for the source directory:");
    string source = Console.ReadLine();

    Console.WriteLine("Enter the output file name (e.g., output.txt):");
    string output = Console.ReadLine();

    Console.WriteLine("Enter the programming languages to include (comma-separated, e.g., csharp,java):");
    string languages = Console.ReadLine();

    Console.WriteLine("Sort files by (name/type):");
    string sort = Console.ReadLine();

    Console.WriteLine("Remove empty lines? (true/false):");
    string removeEmptyLines = Console.ReadLine();

    Console.WriteLine("Enter the author name (or leave blank):");
    string author = Console.ReadLine();

    Console.WriteLine("Include source notes? (true/false):");
    string noteSource = Console.ReadLine();

    // Construct the full command
    string fullCommand = $"bundle --source \"{source}\" --output \"{output}\" --language \"{languages}\" --sort {sort}";

    if (!string.IsNullOrWhiteSpace(removeEmptyLines))
    {
        fullCommand += $" --remove-empty-lines {removeEmptyLines}";
    }

    if (!string.IsNullOrWhiteSpace(author))
    {
        fullCommand += $" --author \"{author}\"";
    }

    if (!string.IsNullOrWhiteSpace(noteSource))
    {
        fullCommand += $" --note {noteSource}";
    }

    // Save to a response file
    Console.WriteLine("Enter the response file name (e.g., command.rsp):");
    string responseFileName = Console.ReadLine();

    File.WriteAllText(responseFileName, fullCommand);

    Console.WriteLine($"Response file created: {responseFileName}");
    Console.WriteLine($"To execute, run: fib @{responseFileName}"); 
});


var rootCommand = new RootCommand("Root command for file Bundler CLI");

rootCommand.AddCommand(bundleCommand);

rootCommand.AddCommand(createRspCommand);

rootCommand.InvokeAsync(args);

 