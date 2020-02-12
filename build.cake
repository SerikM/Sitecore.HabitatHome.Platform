#addin nuget:?package=Cake.Azure&version=0.3.0
#addin nuget:?package=Cake.Http&version=0.7.0
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Cake.Powershell&version=0.4.8
#addin nuget:?package=Cake.XdtTransform&version=0.16.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

#load "local:?path=CakeScripts/helper-methods.cake"
#load "local:?path=CakeScripts/xml-helpers.cake"

var target = Argument<string>("Target", "Default");
var configuration = new Configuration();
var cakeConsole = new CakeConsole();
var configJsonFile = "cake-config.json";
var buildDockerImageScript = $"./scripts/Docker/BuildDockerImage.ps1";
var unicornSyncScript = $"./scripts/Unicorn/Sync.ps1";
var rebuildIndex = true;
/*===============================================
================ MAIN TASKS =====================
===============================================*/

Setup(context =>
{
    cakeConsole.ForegroundColor = ConsoleColor.Yellow;
    var configFile = new FilePath(configJsonFile);
    configuration = DeserializeJsonFromFile<Configuration>(configFile);
    configuration.SolutionFile =  $"{configuration.ProjectFolder}\\{configuration.SolutionName}";
    configuration.UnicornSerializationFolder = "c:\\unicorn"; // This maps to the container's volume setting (see docker-compose.yml)
    configuration.WebsiteRoot = $"{configuration.ProjectFolder}\\data\\cm\\src\\";
    configuration.XConnectRoot = $"{configuration.ProjectFolder}\\data\\xconnect\\src\\";
    configuration.XConnectIndexWorkerRoot = $"{configuration.ProjectFolder}\\data\\xconnect-indexworker\\src\\";
    configuration.InstanceUrl = "http://127.0.0.1:44001";     // This is based on the CM container's settings (see docker-compose.yml)
});

/*===============================================
============ Main Tasks ===========
===============================================*/
Task("Default")
 .IsDependentOn("Base-PreBuild")
 .IsDependentOn("Publish-FrontEnd-Project")
 .IsDependentOn("Build-Solution")
 .IsDependentOn("Publish-Projects-To-Web")
 .IsDependentOn("Sync-Unicorn")
 .IsDependentOn("UpdateDockerImages");
 //.IsDependentOn("Post-Deploy");


// Task("Post-Deploy")

// .IsDependentOn("Deploy-EXM-Campaigns")
//.IsDependentOn("Deploy-Marketing-Definitions")
// .IsDependentOn("Rebuild-Core-Index")
// .IsDependentOn("Rebuild-Master-Index")
// .IsDependentOn("Rebuild-Web-Index")
// .IsDependentOn("Rebuild-Test-Index");


Task("Base-PreBuild").Does(() => {
  CleanDirectories($"{configuration.SourceFolder}/**/obj");
  CleanDirectories($"{configuration.SourceFolder}/**/bin");
});


/*===============================================
=============== Build and publish Tasks =========
===============================================*/
Task("Build-Solution").Does(() => {
  MSBuild(configuration.SolutionFile, cfg => InitializeMSBuildSettings(cfg));
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

Task("Publish-Projects-To-Web").Does(() => {
  PublishWebProjects(configuration, configuration.FeatureSrcFolder, configuration.WebsiteRoot); 
  PublishWebProjects(configuration, configuration.FoundationSrcFolder, configuration.WebsiteRoot);
  PublishWebProjects(configuration, $"{configuration.ProjectSrcFolder}\\Global", configuration.WebsiteRoot); 
  PublishWebProjects(configuration, $"{configuration.ProjectSrcFolder}\\HabitatHome", configuration.WebsiteRoot);   
  PublishProjects($"{configuration.ProjectSrcFolder}\\xConnect", configuration.XConnectRoot);
  PublishProjects($"{configuration.ProjectSrcFolder}\\xConnect", configuration.XConnectIndexWorkerRoot);
});

/*=========================================
==============build a new docker image=====
===========================================*/
Task("UpdateDockerImages")
.Does(() => {
   var publishWebFolder = $"{configuration.ProjectFolder}\\docker\\images\\windows\\cm\\Data";
    // configuration.PublishDataFolder = $"{configuration.ProjectFolder}\\docker\\images\\windows\\sqldev\\Data";
   var publishxConnectFolder = $"{configuration.ProjectFolder}\\docker\\images\\windows\\xconnect\\Data";
   var publishxConnectIndexWorkerFolder = $"{configuration.ProjectFolder}\\docker\\images\\windows\\xconnect-indexworker\\Data";
   var xconnectSourceFolder = $"{configuration.ProjectSrcFolder}\\xConnect";

  PublishWebProjects(configuration, configuration.FeatureSrcFolder, publishWebFolder); 
  PublishWebProjects(configuration, configuration.FoundationSrcFolder, publishWebFolder);
  PublishWebProjects(configuration, $"{configuration.ProjectSrcFolder}\\Global", publishWebFolder); 
  PublishWebProjects(configuration, $"{configuration.ProjectSrcFolder}\\HabitatHome", publishWebFolder);   
  PublishProjects(xconnectSourceFolder, publishxConnectFolder);
  PublishProjects(xconnectSourceFolder, publishxConnectIndexWorkerFolder);

  StartPowershellFile(buildDockerImageScript, new PowershellSettings()
            .SetFormatOutput()
            .SetLogOutput()
            .WithArguments(args => {
              args
              .Append("baseImageName", configuration.CmBaseImageName)
              .Append("tag", configuration.CmImageTagName)
              .Append("buildContext", configuration.BuildContext);
  }));

});
/*==============================================
=============== Unicorn Tasks ===================
===============================================*/
Task("Sync-Unicorn")
.IsDependentOn("Turn-On-Unicorn")
.IsDependentOn("Modify-Unicorn-Source-Folder")
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

/*==============================================
=============== Index Rebuild Tasks ============
===============================================*/
Task("Rebuild-Core-Index")
.WithCriteria(() => rebuildIndex)
.Does(() => {
  RebuildIndex("sitecore_core_index");
});

Task("Rebuild-Master-Index")
.WithCriteria(() => rebuildIndex)
.Does(() => {
  RebuildIndex("sitecore_master_index");
});

Task("Rebuild-Web-Index")
.WithCriteria(() => rebuildIndex).Does(() => {
  RebuildIndex("sitecore_web_index");
});

Task("Rebuild-Test-Index")
.WithCriteria(() => rebuildIndex).Does(() => {
  RebuildIndex("sitecore_testing_index");
});


RunTarget(target);
