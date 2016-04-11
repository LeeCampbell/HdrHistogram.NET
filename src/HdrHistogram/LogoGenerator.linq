<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\WPF\PresentationCore.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\WindowsBase.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Xaml.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\UIAutomationTypes.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Configuration.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\System.Windows.Input.Manipulations.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\UIAutomationProvider.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Deployment.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\PresentationFramework.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\ReachFramework.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\PresentationUI.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\System.Printing.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\Accessibility.dll</Reference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>Rx-Main</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.PlatformServices</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Media</Namespace>
  <Namespace>System.Windows.Controls</Namespace>
  <Namespace>System.Windows.Shapes</Namespace>
  <Namespace>System.Windows.Media.Imaging</Namespace>
  <Namespace>System.Windows</Namespace>
</Query>

void Main()
{
	var stackpanel = new StackPanel { Orientation = Orientation.Horizontal};
	var bar1 = new Rectangle { Width = 23, Height = 32, Fill = new SolidColorBrush(Color.FromRgb(0xB3, 0xC4, 0xE0)), VerticalAlignment = System.Windows.VerticalAlignment.Bottom };//B3C4E0
	var bar2 = new Rectangle { Width = 18, Height = 40, Fill = new SolidColorBrush(Color.FromRgb(0x83, 0xA6, 0xDB)), VerticalAlignment = System.Windows.VerticalAlignment.Bottom };//83A6DB
	var bar3 = new Rectangle { Width = 13, Height = 48, Fill = new SolidColorBrush(Color.FromRgb(0x56, 0x8A, 0xD8)), VerticalAlignment = System.Windows.VerticalAlignment.Bottom };//568AD8
	var bar4 = new Rectangle { Width = 10, Height = 64, Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x6F, 0xD6)), VerticalAlignment = System.Windows.VerticalAlignment.Bottom };//2A6FD6
	stackpanel.Children.Add(bar1);
	stackpanel.Children.Add(bar2);
	stackpanel.Children.Add(bar3);
	stackpanel.Children.Add(bar4);

	var currentDir = System.IO.Path.GetDirectoryName(Util.CurrentQueryPath);
	var releaseDir = System.IO.Path.Combine(currentDir, @"bin\release\");
	
	var outputPath = System.IO.Path.Combine(releaseDir, @"HdrHistogram-icon-64x64.png");
	SaveAsPng(stackpanel, 64, 64, outputPath);
	new FileInfo(outputPath).Length.Dump("FileSize (bytes)");
}

public void SaveAsPng(StackPanel panel, int maxImageWidth, int maxImageHeight, string outputPath)
{
	var viewbox = new Viewbox();
	viewbox.Stretch = Stretch.Uniform;
	viewbox.StretchDirection = StretchDirection.Both;
	viewbox.Child = panel;
	viewbox.Arrange(new Rect(new Point(0, 0), new Point(maxImageWidth, maxImageHeight)));

	var bitmapFrame = CaptureVisual(viewbox);
	SaveToDisk(bitmapFrame, new PngBitmapEncoder(), outputPath);
	viewbox.Child = null;
}
private static BitmapFrame CaptureVisual(UIElement source)
{
	var actualHeight = source.RenderSize.Height;
	var actualWidth = source.RenderSize.Width;

	var renderTarget = new RenderTargetBitmap((int)actualWidth, (int)actualHeight, 96, 96, PixelFormats.Pbgra32);
	var sourceBrush = new VisualBrush(source);
	var drawingVisual = new DrawingVisual();
	var drawingContext = drawingVisual.RenderOpen();

	using (drawingContext)
	{
		drawingContext.DrawRectangle(sourceBrush, null, new Rect(new Point(0, 0), new Point(actualWidth, actualHeight)));
	}
	renderTarget.Render(drawingVisual);
	return BitmapFrame.Create(renderTarget);
}

private static void SaveToDisk(BitmapFrame bitmapFrame, BitmapEncoder bitmapEncoder, string filename)
{
	bitmapEncoder.Frames.Add(bitmapFrame);
	using (var outputStream = File.OpenWrite(filename))
	{
		bitmapEncoder.Save(outputStream);
		outputStream.Flush();
	}
}