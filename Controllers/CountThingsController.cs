using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;



[ApiController]
[Route("umbraco/api/countthings")]
public class CountThingsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IFileService _fileService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IMemberService _memberService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly IScopeProvider _scopeProvider;

    public CountThingsController(
        IUserService userService,
        IContentTypeService contentTypeService,
        IFileService fileService,
        IMediaTypeService mediaTypeService,
        IMemberTypeService memberTypeService,
        IMemberService memberService,
        IDataTypeService dataTypeService,
        IScopeProvider scopeProvider)
    {
        _userService = userService;
        _contentTypeService = contentTypeService;
        _fileService = fileService;
        _mediaTypeService = mediaTypeService;
        _memberTypeService = memberTypeService;
        _memberService = memberService;
        _dataTypeService = dataTypeService;
        _scopeProvider = scopeProvider;
    }

    [HttpGet("content")]
    public IActionResult GetContentCount()
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var db = scope.Database;

        var counts = db.Fetch<dynamic>(@"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN d.published = 1 AND n.trashed = 0 THEN 1 ELSE 0 END) AS Published,
                SUM(CASE WHEN d.published = 0 AND n.trashed = 0 THEN 1 ELSE 0 END) AS Unpublished,
                SUM(CASE WHEN n.trashed = 1 THEN 1 ELSE 0 END) AS Trashed
            FROM umbracoDocument d
            INNER JOIN umbracoNode n ON d.nodeId = n.id").First();

        var redirectCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoRedirectUrl");

        var doctypeRows = db.Fetch<dynamic>(@"
            SELECT ct.alias AS Alias, COUNT(*) AS Cnt
            FROM umbracoDocument d
            INNER JOIN umbracoNode n ON d.nodeId = n.id
            INNER JOIN umbracoContent c ON d.nodeId = c.nodeId
            INNER JOIN cmsContentType ct ON c.contentTypeId = ct.nodeId
            WHERE n.trashed = 0
            GROUP BY ct.alias
            ORDER BY ct.alias");

        var contentTypes = new Dictionary<string, int>();
        foreach (var row in doctypeRows)
            contentTypes[row.Alias] = (int)row.Cnt;

        return Ok(new
        {
            total = (int)counts.Total,
            published = (int)counts.Published,
            unpublished = (int)counts.Unpublished,
            trashed = (int)counts.Trashed,
            redirects = redirectCount,
            contentTypes
        });
    }




    [HttpGet("media")]
    public IActionResult GetMediaCount()
    {
        var mediaGuid = new Guid("B796F64C-1F99-4FFB-B886-4BF4BC011A9C");

        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var db = scope.Database;

        // Query A — Media type breakdown (gives total + folders + mediaTypes dict)
        var mediaTypeRows = db.Fetch<dynamic>(@"
            SELECT ct.alias AS Alias, COUNT(*) AS Cnt
            FROM umbracoNode n
            INNER JOIN umbracoContent c ON n.id = c.nodeId
            INNER JOIN cmsContentType ct ON c.contentTypeId = ct.nodeId
            WHERE n.nodeObjectType = @0
            GROUP BY ct.alias", mediaGuid);

        int total = 0, folders = 0;
        var mediaTypes = new Dictionary<string, int>();
        foreach (var row in mediaTypeRows)
        {
            string alias = row.Alias;
            int cnt = (int)row.Cnt;
            total += cnt;
            if (alias == "Folder") folders = cnt;
            mediaTypes[alias] = cnt;
        }

        // Query B — Category counts (images/videos/audios/other)
        var imageAliases = new HashSet<string> { "Image", "umbracoMediaVectorGraphics" };
        var imageExtensions = new HashSet<string> { "jpg", "jpeg", "png", "gif", "bmp", "tiff", "svg", "webp" };
        var videoAliases = new HashSet<string> { "umbracoMediaVideo" };
        var videoExtensions = new HashSet<string> { "mp4", "mov", "avi", "wmv", "mkv", "mpeg", "mpg", "webm" };
        var audioAliases = new HashSet<string> { "umbracoMediaAudio" };
        var audioExtensions = new HashSet<string> { "mp3", "wav", "ogg", "flac", "aac", "m4a" };

        var categoryRows = db.Fetch<dynamic>(@"
            SELECT ct.alias AS MediaType,
                LOWER(LTRIM(RTRIM(COALESCE(pd.varcharValue, '')))) AS Extension,
                COUNT(*) AS Cnt
            FROM umbracoNode n
            INNER JOIN umbracoContent c ON n.id = c.nodeId
            INNER JOIN cmsContentType ct ON c.contentTypeId = ct.nodeId
            INNER JOIN umbracoContentVersion cv ON n.id = cv.nodeId AND cv.[current] = 1
            LEFT JOIN umbracoPropertyData pd ON cv.id = pd.versionId
                AND pd.propertyTypeId IN (SELECT id FROM cmsPropertyType WHERE alias = 'umbracoExtension')
            WHERE n.nodeObjectType = @0 AND ct.alias <> 'Folder'
            GROUP BY ct.alias, LOWER(LTRIM(RTRIM(COALESCE(pd.varcharValue, ''))))", mediaGuid);

        int images = 0, videos = 0, audios = 0, other = 0;
        foreach (var row in categoryRows)
        {
            string alias = row.MediaType;
            string ext = row.Extension;
            int cnt = (int)row.Cnt;

            if (imageAliases.Contains(alias) || imageExtensions.Contains(ext))
                images += cnt;
            else if (videoAliases.Contains(alias) || videoExtensions.Contains(ext))
                videos += cnt;
            else if (audioAliases.Contains(alias) || audioExtensions.Contains(ext))
                audios += cnt;
            else
                other += cnt;
        }

        // Query C — File type breakdown
        var fileTypeRows = db.Fetch<dynamic>(@"
            SELECT LOWER(LTRIM(RTRIM(pd.varcharValue))) AS Extension, COUNT(*) AS Cnt
            FROM umbracoNode n
            INNER JOIN umbracoContentVersion cv ON n.id = cv.nodeId AND cv.[current] = 1
            INNER JOIN umbracoPropertyData pd ON cv.id = pd.versionId
            INNER JOIN cmsPropertyType pt ON pd.propertyTypeId = pt.id
            WHERE n.nodeObjectType = @0
              AND pt.alias = 'umbracoExtension'
              AND pd.varcharValue IS NOT NULL AND LTRIM(RTRIM(pd.varcharValue)) <> ''
            GROUP BY LOWER(LTRIM(RTRIM(pd.varcharValue)))", mediaGuid);

        var fileTypes = new Dictionary<string, int>();
        foreach (var row in fileTypeRows)
            fileTypes[row.Extension] = (int)row.Cnt;

        // TRY_CAST on SQL Server returns NULL for non-numeric data instead of throwing;
        // SQLite CAST is already lenient (returns 0 for non-numeric)
        var isSqlite = db.DatabaseType.GetType().Name.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0;
        var castBigint = isSqlite ? "CAST" : "TRY_CAST";

        // Query D — Large files (>2MB, non-folders)
        // umbracoBytes uses Label (bigint) which stores in varcharValue on all DB providers
        // (intValue is 32-bit int, too small for file sizes, so Umbraco uses Nvarchar storage)
        var largeFiles = db.ExecuteScalar<int>(@"
            SELECT COUNT(*)
            FROM umbracoNode n
            INNER JOIN umbracoContent c ON n.id = c.nodeId
            INNER JOIN cmsContentType ct ON c.contentTypeId = ct.nodeId
            INNER JOIN umbracoContentVersion cv ON n.id = cv.nodeId AND cv.[current] = 1
            INNER JOIN umbracoPropertyData pd ON cv.id = pd.versionId
            INNER JOIN cmsPropertyType pt ON pd.propertyTypeId = pt.id
            WHERE n.nodeObjectType = @0
              AND pt.alias = 'umbracoBytes'
              AND pd.varcharValue IS NOT NULL AND pd.varcharValue <> ''
              AND " + castBigint + @"(pd.varcharValue AS BIGINT) > 2097152
              AND ct.alias <> 'Folder'", mediaGuid);

        // Query E — Total storage size
        var totalSize = db.ExecuteScalar<long>(@"
            SELECT COALESCE(SUM(" + castBigint + @"(pd.varcharValue AS BIGINT)), 0)
            FROM umbracoNode n
            INNER JOIN umbracoContentVersion cv ON n.id = cv.nodeId AND cv.[current] = 1
            INNER JOIN umbracoPropertyData pd ON cv.id = pd.versionId
            INNER JOIN cmsPropertyType pt ON pd.propertyTypeId = pt.id
            WHERE n.nodeObjectType = @0
              AND pt.alias = 'umbracoBytes'
              AND pd.varcharValue IS NOT NULL AND pd.varcharValue <> ''", mediaGuid);

        return Ok(new
        {
            total,
            folders,
            images,
            videos,
            audios,
            largeFiles,
            other,
            fileTypes,
            mediaTypes,
            totalSize
        });
    }








    [HttpGet("users")]
    public IActionResult GetUserCount()
    {
        var totalUsers = _userService.GetAll(0, int.MaxValue, out _).Count();
        var activeUsers = _userService.GetAll(0, int.MaxValue, out _).Count(u => u.IsApproved);
        var lockedUsers = _userService.GetAll(0, int.MaxValue, out _).Count(u => u.IsLockedOut);
        
        int totalUserGroups = 0;

        var spRoot = StaticServiceProvider.Instance;
        
        Type userGroupSvcType = Type.GetType("Umbraco.Cms.Core.Services.IUserGroupService, Umbraco.Cms.Core")
                                ?? Type.GetType("Umbraco.Cms.Core.Security.IUserGroupService, Umbraco.Cms.Core")
                                ?? Type.GetType("Umbraco.Cms.Core.Services.UserGroupService, Umbraco.Cms.Core");

        if (userGroupSvcType != null)
        {         
            var scopeFactory = spRoot.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            using var diScope = scopeFactory?.CreateScope();
            var sp = diScope?.ServiceProvider ?? spRoot;

            var userGroupSvc = sp.GetService(userGroupSvcType);
            if (userGroupSvc != null)
            {                
                var getAllAsync = userGroupSvcType.GetMethod("GetAllAsync", new[] { typeof(int), typeof(int) });
                if (getAllAsync != null)
                {
                    try
                    {
                        var task = getAllAsync.Invoke(userGroupSvc, new object[] { 0, int.MaxValue });
                        var taskType = task?.GetType();
                        var resultProp = taskType?.GetProperty("Result");
                        var result = resultProp?.GetValue(task);
                        if (result != null)
                        {
                            var totalProp = result.GetType().GetProperty("Total");
                            if (totalProp != null)
                            {
                                totalUserGroups = Convert.ToInt32(totalProp.GetValue(result) ?? 0);
                                goto DoneUserGroups;
                            }
                            var itemsProp = result.GetType().GetProperty("Items");
                            var items = itemsProp?.GetValue(result) as System.Collections.IEnumerable;
                            if (items != null)
                            {
                                totalUserGroups = items.Cast<object>().Count();
                                goto DoneUserGroups;
                            }
                        }
                    }
                    catch { }
                }
                
                var getAllSync = userGroupSvcType.GetMethod("GetAll", Type.EmptyTypes);
                if (getAllSync != null)
                {
                    try
                    {
                        var groupsObj = getAllSync.Invoke(userGroupSvc, null) as System.Collections.IEnumerable;
                        if (groupsObj != null)
                        {
                            totalUserGroups = groupsObj.Cast<object>().Count();
                            goto DoneUserGroups;
                        }
                    }
                    catch { }
                }
                
                var getMany = userGroupSvcType.GetMethod("GetMany", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() });
                if (getMany != null)
                {
                    try
                    {
                        object[] args = { 0, int.MaxValue, 0 };
                        var manyResult = getMany.Invoke(userGroupSvc, args) as System.Collections.IEnumerable;
                        if (manyResult != null)
                        {
                            totalUserGroups = manyResult.Cast<object>().Count();
                            goto DoneUserGroups;
                        }
                    }
                    catch { }
                }
            }
        }
        
        {
            var getAllUG = typeof(Umbraco.Cms.Core.Services.IUserService)
                .GetMethod("GetAllUserGroups", new[] { typeof(int[]) });
            if (getAllUG != null)
            {
                try
                {
                    var groups = getAllUG.Invoke(_userService, new object[] { Array.Empty<int>() }) as System.Collections.IEnumerable;
                    if (groups != null)
                        totalUserGroups = groups.Cast<object>().Count();
                }
                catch { }
            }
        }
    
    DoneUserGroups:
        if (totalUserGroups == 0)
        {
            try
            {
                using var s = _scopeProvider.CreateScope();
                totalUserGroups = s.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoUserGroup");
                s.Complete();
            }
            catch { }
        }

        var totalMembers = _memberService.GetAll(0, int.MaxValue, out _).Count();
        var totalMemberGroups = _memberService.GetAllRoles().Count();

        return Ok(new
        {
            total = totalUsers,
            active = activeUsers,
            locked = lockedUsers,
            userGroups = totalUserGroups,  
            members = totalMembers,
            memberGroups = totalMemberGroups
        });
    }


    [HttpGet("schema")]
    public IActionResult GetSchemaCount()
    {
        var totalDocTypes = _contentTypeService.GetAll().Count();
        var totalTemplates = _fileService.GetTemplates().Count();
        var totalPartials = _fileService.GetPartialViews().Count();
        var totalScripts = _fileService.GetScripts().Count();
        var totalStylesheets = _fileService.GetStylesheets().Count();
        var totalMediaTypes = _mediaTypeService.GetAll().Count();
        var totalMemberTypes = _memberTypeService.GetAll().Count();
        var totalDataTypes = _dataTypeService.GetAll().Count();
        
        int totalLanguages = 0;
        
        using var umbScope = _scopeProvider.CreateScope(autoComplete: true);
        var root = StaticServiceProvider.Instance;
        var scopeFactory = root.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
        using var diScope = scopeFactory?.CreateScope();
        var sp = diScope?.ServiceProvider ?? root;
        
        var langServiceType = Type.GetType("Umbraco.Cms.Core.Services.ILanguageService, Umbraco.Cms.Core");
        if (langServiceType != null)
        {
            var langService = sp.GetService(langServiceType);
            if (langService != null)
            {                
                var getAll = langServiceType.GetMethod("GetAll", Type.EmptyTypes);
                if (getAll != null)
                {
                    try
                    {
                        var langs = getAll.Invoke(langService, null) as System.Collections.IEnumerable;
                        if (langs != null) totalLanguages = langs.Cast<object>().Count();
                    }
                    catch { }
                }
                
                if (totalLanguages == 0)
                {
                    var getAllAsync = langServiceType.GetMethod("GetAllAsync", Type.EmptyTypes)
                                      ?? langServiceType.GetMethod("GetAllAsync", new[] { typeof(System.Threading.CancellationToken) });

                    if (getAllAsync != null)
                    {
                        try
                        {
                            object taskObj = getAllAsync.GetParameters().Length == 1
                                ? getAllAsync.Invoke(langService, new object[] { default(System.Threading.CancellationToken) })
                                : getAllAsync.Invoke(langService, null);

                            if (taskObj is System.Threading.Tasks.Task t)
                            {
                                t.GetAwaiter().GetResult();
                                var resultProp = t.GetType().GetProperty("Result");
                                var result = resultProp?.GetValue(t);
                                
                                if (result is System.Collections.IEnumerable en)
                                {
                                    totalLanguages = en.Cast<object>().Count();
                                }
                                else
                                {                                    
                                    var totalProp = result?.GetType().GetProperty("Total");
                                    if (totalProp != null)
                                        totalLanguages = Convert.ToInt32(totalProp.GetValue(result) ?? 0);
                                }
                            }
                        }
                        catch {  }
                    }
                }
            }
        }
        
        if (totalLanguages == 0)
        {
            var locServiceType = Type.GetType("Umbraco.Cms.Core.Services.ILocalizationService, Umbraco.Cms.Core");
            if (locServiceType != null)
            {
                var locService = sp.GetService(locServiceType);
                var getAllLangs = locServiceType.GetMethod("GetAllLanguages", Type.EmptyTypes);
                if (locService != null && getAllLangs != null)
                {
                    try
                    {
                        var langs = getAllLangs.Invoke(locService, null) as System.Collections.IEnumerable;
                        if (langs != null) totalLanguages = langs.Cast<object>().Count();
                    }
                    catch { }
                }
            }
        }
        
        if (totalLanguages == 0)
        {
            try
            {
                totalLanguages = umbScope.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoLanguage");
            }
            catch { }
        }        

        var totalSchema = totalDocTypes + totalTemplates + totalPartials + totalScripts +
                          totalStylesheets + totalMediaTypes + totalMemberTypes +
                          totalDataTypes + totalLanguages;

        return Ok(new
        {
            total = totalSchema,
            doctypes = totalDocTypes,
            templates = totalTemplates,
            partials = totalPartials,
            scripts = totalScripts,
            stylesheets = totalStylesheets,
            mediatypes = totalMediaTypes,
            membertypes = totalMemberTypes,
            datatypes = totalDataTypes,
            languages = totalLanguages
        });
    }


    [HttpGet("forms")]
    public IActionResult GetFormsCount()
    {        
        var hasFormsAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(a => a.GetName().Name.Equals("Umbraco.Forms.Core", StringComparison.OrdinalIgnoreCase));

        if (!hasFormsAssembly)
            return Ok(new { installed = false, total = 0, entries = 0 });
        
        var formRepoType = Type.GetType("Umbraco.Forms.Core.Persistence.Repositories.IFormRepository, Umbraco.Forms.Core");
        var recordReaderType = Type.GetType("Umbraco.Forms.Core.Services.IRecordReaderService, Umbraco.Forms.Core");

        using var umbScope = _scopeProvider.CreateScope(autoComplete: true);

        var root = StaticServiceProvider.Instance;
        var scopeFactory = root.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
        using var diScope = scopeFactory?.CreateScope();
        var sp = diScope?.ServiceProvider ?? root; 

        object formRepo = formRepoType != null ? sp.GetService(formRepoType) : null;
        object recordReader = recordReaderType != null ? sp.GetService(recordReaderType) : null;

        int formCount = 0;
        long entryCount = 0;
       
        if (formRepo != null)
        {
            var getMany = formRepoType.GetMethod("GetMany", Type.EmptyTypes);
            var formsObj = getMany?.Invoke(formRepo, null) as System.Collections.IEnumerable;
            var forms = formsObj?.Cast<object>().ToList() ?? new List<object>();
            formCount = forms.Count;
            
            if (recordReader != null)
            {
                var readerMethod = recordReaderType.GetMethod(
                    "GetRecordsFromForm",
                    new[] { typeof(Guid), typeof(int), typeof(int) });

                foreach (var formEntity in forms)
                {
                    if (formEntity is null) continue;
                    
                    var keyProp = formEntity.GetType().GetProperty("Key")
                              ?? formEntity.GetType().GetProperty("Id")
                              ?? formEntity.GetType().GetProperty("UniqueId");

                    if (keyProp == null) continue;
                    if (keyProp.GetValue(formEntity) is Guid key && readerMethod != null)
                    {
                        try
                        {
                            var pageObj = readerMethod.Invoke(recordReader, new object[] { key, 1, 1 });
                            var totalItemsProp = pageObj?.GetType().GetProperty("TotalItems");
                            if (totalItemsProp != null)
                                entryCount += Convert.ToInt64(totalItemsProp.GetValue(pageObj) ?? 0L);
                        }
                        catch
                        {                            
                        }
                    }
                }
            }
        }

        
        if (formCount == 0)
        {
            try { formCount = Convert.ToInt32(umbScope.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM UFForms")); }
            catch {  }
        }

        if (entryCount == 0 && formCount > 0)
        {
            try { entryCount = umbScope.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM UFRecords"); }
            catch {  }
        }

        return Ok(new { installed = true, total = formCount, entries = entryCount });
    }





}
