using System.Reflection;
using HotkeyManager.Core;
using Microsoft.Web.WebView2.WinForms;
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var assembly = typeof(HotkeyParser).Assembly;
        Console.WriteLine(string.Join("\n", assembly.GetManifestResourceNames()));
        var type = assembly.GetType("HotkeyManager.Settings.SettingsForm")!;
        var path = Path.Combine(Path.GetTempPath(), "hotkey-window-check-" + Guid.NewGuid() + ".jsonc");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { hotkeys = Enumerable.Range(1, 24).Select(i => new { name = "测试应用 " + i, processName = "notepad", exePath = "notepad.exe", key = "" }) }));
        using var form = (Form)Activator.CreateInstance(type, path, (Action<bool>)(_ => { }))!;
        var view = (WebView2)type.GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        using var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        int ticks = 0; bool checking = false; bool hooked = false;
        timer.Tick += async (_, _) => {
            if (checking) return; checking = true;
            try {
                ticks++;
                if (view.CoreWebView2 is { } core) {
                    if (!hooked) { hooked = true; core.NavigationStarting += (_, e) => Console.WriteLine($"NAV {e.Uri.Split(',')[0]} Cancel={e.Cancel}"); core.NavigationCompleted += (_, e) => Console.WriteLine($"DONE {e.IsSuccess} {e.WebErrorStatus}"); }
                    var content = await core.ExecuteScriptAsync("document.documentElement.outerHTML");
                    Console.WriteLine($"tick {ticks} source={core.Source.Split(',')[0]} html={content.Substring(0, Math.Min(200, content.Length))}");
                    if (content.Contains("searchcount")) {
                        var state = await core.ExecuteScriptAsync("JSON.stringify({title:document.querySelector('h1')?.textContent,count:document.getElementById('searchcount')?.textContent,ready:typeof send})");
                        Console.WriteLine("PAGE " + state);
                        var layout = await core.ExecuteScriptAsync("""
                            (() => {
                              const d=document.documentElement,b=document.body, del=document.getElementById('delete'),save=document.getElementById('save');
                              const action=save.parentElement.getBoundingClientRect(), footer=document.querySelector('.footer').getBoundingClientRect();
                              const style=getComputedStyle(del);
                              const checks={title:document.querySelector('h1').textContent==='快捷键设置',count:document.getElementById('searchcount').textContent==='24 个应用',page:document.querySelectorAll('#list .item').length===6,noScroll:d.scrollHeight<=innerHeight && d.scrollWidth<=innerWidth && b.scrollHeight<=innerHeight && b.scrollWidth<=innerWidth,footerVisible:footer.bottom<=innerHeight,buttonsVisible:save.getBoundingClientRect().bottom<=innerHeight && del.getBoundingClientRect().bottom<=action.bottom+1,textButtonsBorderless:style.borderTopWidth==='0px'&&getComputedStyle(document.getElementById('reset')).borderTopWidth==='0px'};
                              return JSON.stringify({checks,pass:Object.values(checks).every(Boolean),viewport:[innerWidth,innerHeight],actions:[action.bottom,del.getBoundingClientRect().bottom,save.getBoundingClientRect().bottom]});
                            })()
                            """);
                        Console.WriteLine("LAYOUT " + layout);
                        using var layoutResult = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Deserialize<string>(layout)!);
                        Environment.ExitCode = layoutResult.RootElement.GetProperty("pass").GetBoolean() ? 0 : 2;
                        await core.ExecuteScriptAsync("document.getElementById('delete').click()");
                        var modal = await core.ExecuteScriptAsync("""
                            (()=>{
                              const dialog=document.getElementById('confirm-dialog'),r=dialog.getBoundingClientRect();
                              const checks={open:dialog.open,centered:Math.abs(r.x+r.width/2-innerWidth/2)<1&&Math.abs(r.y+r.height/2-innerHeight/2)<1,width:r.width===360,visible:r.top>=0&&r.bottom<=innerHeight,cancelFocused:document.activeElement.id==='confirm-cancel',specificAction:document.getElementById('confirm-accept').textContent==='删除快捷键',description:document.getElementById('confirm-description').textContent.includes('测试应用 1')};
                              const ids=['save','delete','browse','reset','confirm-cancel','confirm-accept'];
                              const rects=ids.map(id=>document.getElementById(id).getBoundingClientRect());
                              checks.controlHeightsConsistent=rects.every(rect=>Math.abs(rect.height-rects[0].height)<1);
                              checks.controlRadiiConsistent=['save','browse','reset','confirm-cancel','confirm-accept','name','path','process'].map(id=>getComputedStyle(document.getElementById(id)).borderRadius).every(radius=>radius===getComputedStyle(document.getElementById('save')).borderRadius);
                              checks.deleteLeftAligned=Math.abs(document.getElementById('delete').getBoundingClientRect().left-document.getElementById('name').getBoundingClientRect().left)<1;
                              const addIcon=document.querySelector('.add-icon').getBoundingClientRect(),addText=document.querySelector('.add-inline span').getBoundingClientRect();
                              checks.addIconTextCentered=Math.abs(addIcon.top+addIcon.height/2-addText.top-addText.height/2)<1;
                              const deleteStyle=getComputedStyle(document.getElementById('delete'));
                              const deleteRect=document.getElementById('delete').getBoundingClientRect(),saveRect=document.getElementById('save').getBoundingClientRect();
                              checks.actionEdgesAligned=Math.abs(deleteRect.top-saveRect.top)<.5&&Math.abs(deleteRect.bottom-saveRect.bottom)<.5;
                              const deleteRange=document.createRange(),saveRange=document.createRange();
                              deleteRange.selectNodeContents(document.getElementById('delete'));saveRange.selectNodeContents(document.getElementById('save'));
                              const deleteText=deleteRange.getBoundingClientRect(),saveText=saveRange.getBoundingClientRect();
                              checks.actionTextAligned=Math.abs(deleteText.top-saveText.top)<.5&&Math.abs(deleteText.bottom-saveText.bottom)<.5;
                              checks.deletePaddingBalanced=deleteStyle.paddingLeft===deleteStyle.paddingRight && deleteStyle.paddingLeft===getComputedStyle(document.getElementById('save')).paddingLeft;
                              checks.pageArrowsCentered=[...document.querySelectorAll('.listpager button')].every(button=>{
                                const b=button.getBoundingClientRect(),icon=button.querySelector('svg')?.getBoundingClientRect();
                                return icon&&Math.abs(icon.left+icon.width/2-b.left-b.width/2)<.5&&Math.abs(icon.top+icon.height/2-b.top-b.height/2)<.5;
                              });
                              checks.panelPaddingConsistent=getComputedStyle(document.querySelector('.panelheader')).paddingLeft===getComputedStyle(document.querySelector('.form')).paddingLeft;
                              checks.buttonTypographyConsistent=['save','delete','browse','reset','confirm-cancel','confirm-accept'].map(id=>getComputedStyle(document.getElementById(id)).fontSize).filter((size,i)=>i!==2).every(size=>size===getComputedStyle(document.getElementById('save')).fontSize);
                              checks.panelAboveFooter=document.querySelector('.panel').getBoundingClientRect().bottom<=document.querySelector('.footer').getBoundingClientRect().top;
                              document.getElementById('confirm-cancel').click();
                              checks.cancelled=!dialog.open;
                              return JSON.stringify({checks,pass:Object.values(checks).every(Boolean)});
                            })()
                            """);
                        Console.WriteLine("MODAL " + modal);
                        using var modalResult = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Deserialize<string>(modal)!);
                        if (!modalResult.RootElement.GetProperty("pass").GetBoolean()) Environment.ExitCode=2;
                        var runningService = assembly.GetType("HotkeyManager.Settings.RunningApplicationService")!;
                        var actualApp = runningService.GetMethod("Resolve")!.Invoke(null, new object[] { Environment.ProcessId })!;
                        var resolvedPath = (string)actualApp.GetType().GetProperty("path")!.GetValue(actualApp)!;
                        var resolvedProcess = (string)actualApp.GetType().GetProperty("process")!.GetValue(actualApp)!;
                        if (!File.Exists(resolvedPath) || resolvedProcess != System.Diagnostics.Process.GetCurrentProcess().ProcessName) Environment.ExitCode=2;
                        Console.WriteLine("RUNNING APP path/process resolved: " + File.Exists(resolvedPath));
                        await core.ExecuteScriptAsync("document.getElementById('running').click()");
                        for (var attempt=0; attempt<30; attempt++)
                        {
                            await Task.Delay(100);
                            var loading=await core.ExecuteScriptAsync("runningBusy");
                            if(loading=="false") break;
                        }
                        await core.ExecuteScriptAsync("runningApps=Array.from({length:20},(_,i)=>({id:i+1,name:'应用'+i,process:'proc'+i,path:'app.exe',title:'窗口'+i,error:null}));runningBusy=false;renderRunningApps()");
                        var picker=await core.ExecuteScriptAsync("""
                            (()=>{
                              const dialog=document.getElementById('running-dialog'),r=dialog.getBoundingClientRect();
                              const checks={open:dialog.open,visible:r.top>=0&&r.bottom<=innerHeight,noScroll:dialog.scrollHeight<=dialog.clientHeight,allEntries:document.querySelectorAll('.running-item').length===20,noPagination:!document.getElementById('running-pager'),listScroll:getComputedStyle(document.getElementById('running-list')).overflowY==='auto'&&document.getElementById('running-list').scrollHeight>document.getElementById('running-list').clientHeight,focused:document.activeElement.id==='running-search',insidePath:document.getElementById('browse').getBoundingClientRect().right<=document.getElementById('path').getBoundingClientRect().right};
                              document.getElementById('running-close').click();checks.cancelled=!dialog.open;
                              return JSON.stringify({checks,pass:Object.values(checks).every(Boolean)});
                            })()
                            """);
                        Console.WriteLine("RUNNING PICKER " + picker);
                        using var pickerResult=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Deserialize<string>(picker)!);
                        if(!pickerResult.RootElement.GetProperty("pass").GetBoolean()) Environment.ExitCode=2;
                        form.Dispose(); Application.ExitThread(); return;
                    }
                }
                if (ticks >= 15) { Environment.ExitCode=1; form.Dispose(); Application.ExitThread(); }
            } catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode=1; form.Dispose(); Application.ExitThread(); }
            finally { checking=false; }
        };
        form.Shown += (_, _) => timer.Start();
        try { Application.Run(form); } finally { File.Delete(path); }
    }
}
