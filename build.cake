#addin nuget:?package=Cake.Azure&version=0.3.0
#addin nuget:?package=Cake.Http&version=0.7.0
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Cake.Powershell&version=0.4.8
#addin nuget:?package=Cake.XdtTransform&version=0.16.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

#load "local:?path=CakeScripts/helper-methods.cake"
#load "local:?path=CakeScripts/xml-helpers.cake"

var target = Argument<string>("Target", "Default");
var deploymentTarget = Argument<string>("DeploymentTarget", "IIS"); // Possible values are 'IIS', 'Docker' and 'DockerBuild'
var configuration = new Configuration();
var cakeConsole = new CakeConsole();
var configJsonFile = "cake-config.json";
var unicornSyncScript = $"./scripts/Unicorn/Sync.ps1";
bool syncUnicorn = true;
/*===============================================
================ MAIN TASKS =====================
===============================================*/

Setup(context =>
{
  cakeConsole.ForegroundColor = ConsoleColor.Yellow;

  var configFile = new FilePath(configJsonFile);
  configuration = DeserializeJsonFromFile<Configuration>(configFile);
  configuration.SolutionFile =  $"{configuration.ProjectFolder}\\{configuration.SolutionName}";
  configuration.PublishWebFolder = $"{configuration.ProjectFolder}\\data\\cm\\src";
  configuration.PublishxConnectFolder = $"{configuration.ProjectFolder}\\data\\xconnect\\src";

  if (deploymentTarget.Contains("Docker"))  {
    configuration.UnicornSerializationFolder = "c:\\unicorn"; // This maps to the container's volume setting (see docker-compose.yml)
  }

  if (deploymentTarget == "Docker") {
    configuration.WebsiteRoot = $"{configuration.ProjectFolder}\\data\\cm\\src\\";
    configuration.XConnectRoot = $"{configuration.ProjectFolder}\\data\\xconnect\\src\\";
    configuration.PublishxConnectIndexWorkerFolder = $"{configuration.ProjectFolder}\\data\\xconnect-indexworker\\src\\";
    configuration.InstanceUrl = "http://127.0.0.1:44001";     // This is based on the CM container's settings (see docker-compose.yml)
  }
});

/*===============================================
============ Main Tasks ===========
===============================================*/
Task("Default")
 .IsDependentOn("Base-PreBuild")
 .IsDependentOn("Base-Publish")
 .IsDependentOn("Post-Deploy");

Task("Base-Publish")
 //.IsDependentOn("Publish-FrontEnd-Project")
 .IsDependentOn("Publish-All-Projects")
 .IsDependentOn("Publish-xConnect-Project")
 .IsDependentOn("Publish-xConnect-Project-IndexWorker");

Task("Post-Deploy")
   .IsDependentOn("Sync-Unicorn");
// .IsDependentOn("Deploy-EXM-Campaigns")
// .IsDependentOn("Deploy-Marketing-Definitions")
// .IsDependentOn("Rebuild-Core-Index")
// .IsDependentOn("Rebuild-Master-Index")
// .IsDependentOn("Rebuild-Web-Index")
// .IsDependentOn("Rebuild-Test-Index");


// Task("Quick-Deploy")
// .IsDependentOn("Base-PreBuild")
// .IsDependentOn("Base-Publish");


Task("Base-PreBuild").Does(() => {
  CleanDirectories($"{configuration.SourceFolder}/**/obj");
  CleanDirectories($"{configuration.SourceFolder}/**/bin");
});


/*===============================================
=============== Build and publish Tasks =========
===============================================*/
Task("Publish-All-Projects")
 .IsDependentOn("Build-Solution")
 .IsDependentOn("Publish-Foundation-Projects")
 .IsDependentOn("Publish-Feature-Projects")
 .IsDependentOn("Publish-Project-Projects");

Task("Build-Solution").Does(() => {
  MSBuild(configuration.SolutionFile, cfg => InitializeMSBuildSettings(cfg));
});

Task("Publish-Foundation-Projects").Does(() => {
   var tempFolder = configuration.PublishTempFolder;
   EnsureDirectoryExists(tempFolder);
   PublishProjects(configuration.FoundationSrcFolder, tempFolder);
   var tempFolderPath = tempFolder.ToLower().Replace("\\", "/");
   var ignoredFiles = new string[] {$"{tempFolderPath}/web.config"};
   var contentFiles = GetFiles($"{tempFolder}\\**\\*");
   if(contentFiles.Any()){
     var filteredFiles = contentFiles.Where(file => !ignoredFiles.Contains(file.FullPath.ToLower()));
     CopyFiles(filteredFiles, configuration.WebsiteRoot, preserveFolderStructure: true);
   }
   
});

Task("Publish-Project-Projects").Does(() => {
  var global = $"{configuration.ProjectSrcFolder}\\Global";
  var habitatHome = $"{configuration.ProjectSrcFolder}\\HabitatHome";
  var tempFolder = configuration.PublishTempFolder;
  EnsureDirectoryExists(tempFolder);
  PublishProjects(global, tempFolder);
  PublishProjects(habitatHome, tempFolder);
  var tempFolderPath = tempFolder.ToLower().Replace("\\", "/");
  var ignoredFiles = new string[] {$"{tempFolderPath}/web.config"};
   var contentFiles = GetFiles($"{tempFolder}\\**\\*");
   if(contentFiles.Any()){
     var filteredFiles = contentFiles.Where(file => !ignoredFiles.Contains(file.FullPath.ToLower()));
     CopyFiles(filteredFiles, configuration.WebsiteRoot, preserveFolderStructure: true);
   }
 
});

Task("Publish-Feature-Projects").Does(() => {
   var tempFolder = configuration.PublishTempFolder;
   EnsureDirectoryExists(tempFolder);
   PublishProjects(configuration.FeatureSrcFolder, tempFolder);
   var tempFolderPath = tempFolder.ToLower().Replace("\\", "/");
   var ignoredFiles = new string[] {$"{tempFolderPath}/web.config"};
   var contentFiles = GetFiles($"{tempFolder}\\**\\*");
   if(contentFiles.Any()){
     var filteredFiles = contentFiles.Where(file => !ignoredFiles.Contains(file.FullPath.ToLower()));
     CopyFiles(filteredFiles, configuration.WebsiteRoot, preserveFolderStructure: true);
   }
   
});

Task("Publish-FrontEnd-Project").Does(() => {
  var source = $"{configuration.ProjectFolder}\\FrontEnd\\**\\*";
  var destination = configuration.WebsiteRoot;
  destination = destination + "\\App_Data\\FrontEnd\\-";    
  EnsureDirectoryExists(destination);
  Information("Source: " + source);
  Information("Destination: " + destination);
  var contentFiles = GetFiles(source)
  .Where(file => !file.FullPath.ToLower().Contains("node_modules"));
  CopyFiles(contentFiles, destination, preserveFolderStructure: true);
});

Task("Publish-xConnect-Project").Does(() => {
  var xConnectProject = $"{configuration.ProjectSrcFolder}\\xConnect";
  var destination = configuration.XConnectRoot;
  PublishProjects(xConnectProject, destination);
});

Task("Publish-xConnect-Project-IndexWorker").Does(() => {
  var xConnectProject = $"{configuration.ProjectSrcFolder}\\xConnect";
  var destination = configuration.PublishxConnectIndexWorkerFolder;
  PublishProjects(xConnectProject, destination);
});


/*==============================================
=============== Unicorn Tasks ===================
===============================================*/
Task("Sync-Unicorn")
.IsDependentOn("Turn-On-Unicorn")
.IsDependentOn("Modify-Unicorn-Source-Folder")
.WithCriteria(() => syncUnicorn)
.Does(() => {
  var unicornUrl = configuration.InstanceUrl + "/unicorn.aspx";
  Information("Sync Unicorn items from url: " + unicornUrl);

  var authenticationFile = new FilePath($"{configuration.WebsiteRoot}/App_config/Include/Unicorn/Unicorn.UI.config");
  var xPath = "/configuration/sitecore/unicorn/authenticationProvider/SharedSecret";
  string sharedSecret = XmlPeek(authenticationFile, xPath);

  StartPowershellFile(unicornSyncScript, new PowershellSettings()
            .SetFormatOutput()
            .SetLogOutput()
            .WithArguments(args => {
              args.Append("secret", sharedSecret)
                .Append("url", unicornUrl);
  }));
});

Task("Turn-On-Unicorn")
.WithCriteria(() => (syncUnicorn))
.Does(() => {
  var webConfigFile = File($"{configuration.WebsiteRoot}/app_config/include/project/global.website.config");
  var xmlSetting = new XmlPokeSettings {
    Namespaces = new Dictionary<string, string> {
      {"patch", @"http://www.sitecore.net/xmlconfig/"}
    }
  };
  var unicornAppSettingXPath = "configuration/sitecore/settings/setting[@name='unicorn']";
  XmlPoke(webConfigFile, unicornAppSettingXPath, "Enabled", xmlSetting);
});


Task("Modify-Unicorn-Source-Folder")
.Does(() => {
  var destination = configuration.WebsiteRoot;
  var zzzDevSettingsFile = File($"{destination}/App_config/Include/Project/z.DevSettings.config");
  var sourceFolderXPath = "configuration/sitecore/sc.variable[@name='sourceFolder']/@value";
  var directoryPath = MakeAbsolute(new DirectoryPath(configuration.UnicornSerializationFolder)).FullPath;
  var xmlSetting = new XmlPokeSettings {
    Namespaces = new Dictionary<string, string> {
      {"patch", @"http://www.sitecore.net/xmlconfig/"}
    }
  };
  XmlPoke(zzzDevSettingsFile, sourceFolderXPath, directoryPath, xmlSetting);
});

RunTarget(target);
