# Documentation guidelines

The HdrHsitogram.NET documentation is built in [Markdown](https://help.github.com/articles/markdown-basics/). 
We use a few simple conventions to ensure a homogeneous style throughout the full set of documents.

As per any change to the repo, if you have issues with these guidelines then raise an issue or a [Pull Request](https://help.github.com/articles/using-pull-requests/).
If you find documentation that fails to meet the guidelines, then make a fix and submit a pull request.

#Structure

##Language
The documentation will follow US-English spelling.
Desktop tools like http://markdownpad.com have spell checking features.

##Paragraph structure
Each sentence should be written on a single line, and only one sentence per line.
This makes merging changes easier and also helps identify verbose language.

Paragraphs in Markdown are just one or more lines of consecutive text followed by one or more blank lines.

##Headings
Heading should be used to structure a document.
Avoid using other emphasis features like ALLCAPS, *Italics* or **bold** to identify a new topic. 
Using a header is not only more consistent, but also allows linking to the header.

##Footers
At the end of a page, it is helpful to link to the next logical page in the documentation.
If the page is the last in a sub-section then linking back to the index page is useful. 

#Styles

##Code formatting
Blocks of example code should be formatted with the triple back tick format followed by the language

	``` csharp
	using (var writer = new StreamWriter("HistogramResults.hgrm"))
	{
		histogram.OutputPercentileDistribution(writer);
	}
	```

Which will render as

``` csharp
using (var writer = new StreamWriter("HistogramResults.hgrm"))
{
    histogram.OutputPercentileDistribution(writer);
}
```

Inline code should be marked with a single backtick (\`).

This includes references to:

 * type names e.g. `Task<T>`
 * variable names e.g. `task`
 * namespaces e.g. `System.Threading.Tasks`

If showing text that is an output (e.g. text file content or console output) you can either use the triple back tick without specifying a language or you can indent the content. 
For example:


		   Value     Percentile TotalCount 1/(1-Percentile)

		   0.285 0.000000000000          1           1.00
		   0.535 0.500000000000      17644           2.00
		   0.594 0.750000000000      26260           4.00
		   0.627 0.800000000000      28005           5.00
		   0.660 0.850000000000      29793           6.67
		   0.680 0.875000000000      30649           8.00
		   0.693 0.900000000000      31550          10.00
		   0.703 0.925000000000      32415          13.33
		   0.717 0.950000000000      33277          20.00
		   0.748 0.975000000000      34141          40.00
		   0.869 0.990625000000      34676         106.67
		   2.801 0.999023437500      34970        1024.00
		2543.615 0.999972534180      35004       36408.89
		2543.615 1.000000000000      35004
	#[Mean    =        0.633, StdDeviation   =       13.588]
	#[Max     =     2541.568, Total count    =        35004]
	#[Buckets =           21, SubBuckets     =         2048]

 

##File names and paths
When referencing a filename, directory/folder or URI then use standard italics to format. 
This can be done by surrounding the string with either with a single asterisk (`*`) or a single underscore (`_`) 

Examples:

* *HdrHistogram.dll*
* *C:\Binaries*
* *.\GoogleChartsExample\plotFiles.html*


##Tables

Markdown supports [tabular data](https://help.github.com/articles/github-flavored-markdown/#tables).
Tables could be used to structure data so that is is easily consumable for the reader.

Suffix |     Unit 
-------|-------------
ms     | millisecond(s)  
s      | second(s)  
m      | minute(s)    


##Links 
When referencing another concept, the concept should be linked to.
Forward and backward references with in a page can be linked to via the header. e.g. link back to [Structure](#structure)
Links to other documents can either link to the page, or a sub-section/header within the page. 
External links should be exposed as a the full link e.g. https://github.com/dotnet/roslyn



#Contribution
The Orleans documentation is managed as Markdown files in a Git repository hosted on [Github in the gh-pages branch](https://github.com/dotnet/orleans/tree/gh-pages).
See the [GitHub Pages](https://pages.github.com/) documentation on how to use the `gh-pages` branch convention for "Project site" documents.