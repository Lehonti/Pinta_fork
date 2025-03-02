using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Xml;

// Create a map from the header properties to their untranslated values, e.g. "Name" => "MyEffect"
static Dictionary<string, string> ExtractHeaderProperties (XmlDocument manifestDoc)
{
	// The properties that we care about translating.
	string[] headerProperties = ["Name", "Description"];

	Dictionary<string, string> propertyMap = new ();
	foreach (string propertyName in headerProperties) {
		XmlNode? node = manifestDoc.SelectSingleNode ($"/Addin/Header/{propertyName}");
		if (node is null)
			throw new InvalidDataException ($"Add-in manifest does not specify header property '{propertyName}'");

		propertyMap[node.Name] = node.InnerText;
	}

	return propertyMap;
}

// Insert translations from the resource files into the manifest (.addin.xml)
// See https://github.com/mono/mono-addins/wiki/The-add-in-header
static void LocalizeManifest (FileInfo manifestFile, FileInfo[] resourceFiles)
{
	Console.WriteLine ($"Loading manifest from {manifestFile}");
	XmlDocument manifestDoc = new () { PreserveWhitespace = true };
	manifestDoc.Load (manifestFile.FullName);

	Dictionary<string, string> headerProperties = ExtractHeaderProperties (manifestDoc);
	XmlNode addinHeaderNode = manifestDoc.SelectSingleNode ("/Addin/Header")
		?? throw new InvalidDataException ("Failed to find addin header node");

	foreach (var resourceFile in resourceFiles) {
		// Parse the locale name from filenames like Language.es.resx.
		// We don't need to process the template file (Language.resx).
		var components = resourceFile.Name.Split ('.');
		if (components.Length != 3) {
			Console.WriteLine ($"Skipping file {resourceFile}");
			continue;
		}

		string langCode = components[1];

		Console.WriteLine ($"{langCode}: Loading resource {resourceFile}");
		var resourceDoc = new XmlDocument ();
		resourceDoc.Load (resourceFile.FullName);

		foreach ((string propertyName, string propertyText) in headerProperties) {
			XmlNode? translationNode = resourceDoc.SelectSingleNode ($"/root/data[@name='{propertyText}']/value");
			if (translationNode is not null) {
				Console.WriteLine ($" - Adding translation for {propertyName}: {translationNode.InnerText}");

				// Add a sibling node, e.g. <Name locale="es">Translated string</Name>
				var newNode = manifestDoc.CreateElement (propertyName);
				newNode.SetAttribute ("locale", langCode);
				newNode.InnerText = translationNode.InnerText;

				addinHeaderNode.AppendChild (newNode);
			} else
				Console.WriteLine ($" - Did not find translation for {propertyName}");
		}
	}

	Console.WriteLine ($"Updating {manifestFile}");
	manifestDoc.Save (manifestFile.FullName);
}

var manifestFileOption =
	new Option<FileInfo> (name: "--manifest-file") { IsRequired = true }
	.ExistingOnly ();

var resourceFilesOption =
	new Option<FileInfo[]> (name: "--resource-files") {
		IsRequired = true,
		AllowMultipleArgumentsPerToken = true,
	}
	.ExistingOnly ();

Command localizeManifestCommand = new (
	name: "localize-manifest",
	description: "Copy translations from resource files into the add-in manifest")
{
	manifestFileOption,
	resourceFilesOption,
};
localizeManifestCommand.SetHandler (LocalizeManifest, manifestFileOption, resourceFilesOption);

RootCommand rootCommand = new ("Command-line utilities for Pinta add-ins.");
rootCommand.AddCommand (localizeManifestCommand);
rootCommand.Invoke (args);
