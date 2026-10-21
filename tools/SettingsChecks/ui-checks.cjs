const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
class Element {
  constructor() { this.value = ''; this.children = []; this.style = { setProperty() {} }; this.classList = { add() {}, remove() {} }; this.dataset = {}; this.open = false; this.disabled = false; this.textContent = ''; }
  replaceChildren(...children) { this.children = children; }
  append(...children) { this.children.push(...children); }
  focus() {} select() {}
  showModal() { this.open=true; } close() { this.open=false; this.onclose?.(); }
}
const ids = ['toast','record','recordhint','list','search','searchcount','name','path','process','error','add','reset','save','delete','browse','listpager','confirm-dialog','confirm-title','confirm-description','confirm-cancel','confirm-accept','running','running-dialog','running-search','running-close','running-refresh','running-status','running-list'];
const elements = Object.fromEntries(ids.map(id => [id, new Element()]));
const sent = []; let receive, keydown;
const context = vm.createContext({ document: { getElementById: id => elements[id], createElement: () => new Element(), querySelectorAll: () => [], addEventListener: (type, fn) => { if(type === 'keydown') keydown = fn; } }, window: { chrome: { webview: { postMessage: m => sent.push(m), addEventListener: (_, fn) => receive = fn } } }, setTimeout: () => 1, clearTimeout() {} });
vm.runInContext(fs.readFileSync('src/HotkeyManager/Settings/settings.js', 'utf8'), context);
(async () => {
assert.equal(sent.at(-1).type, 'load');
receive({data:{type:'loaded',entries:[]}}); assert.equal(elements.searchcount.textContent, '0 个应用');
await elements.add.onclick(); elements.name.value='记事本'; elements.path.value='C:\\Windows\\notepad.exe'; elements.process.value='notepad';
elements.record.onclick(); assert.equal(sent.at(-1).active,true);
keydown({key:'N',code:'KeyN',ctrlKey:true,altKey:true,shiftKey:false,metaKey:false,preventDefault(){},stopPropagation(){}});
assert.equal(sent.at(-1).active,false);
elements.save.onclick(); const save=sent.at(-1); assert.equal(save.type,'save'); assert.equal(save.entries[0].key,'Ctrl+Alt+N'); assert.equal(save.entries[0]._new,undefined);
receive({data:{type:'error',message:'占用'}}); assert.equal(elements.error.textContent,'占用');
elements.save.onclick(); receive({data:{type:'saved'}}); assert.equal(elements.searchcount.textContent,'1 个应用');
elements.name.value='未保存'; elements.name.oninput();
const addPending=elements.add.onclick(); assert.equal(elements['confirm-dialog'].open,true); assert.equal(elements['confirm-cancel'].textContent,'继续编辑'); elements['confirm-cancel'].onclick(); await addPending; assert.equal(elements.searchcount.textContent,'1 个应用');
const closePending=receive({data:{type:'closing'}}); assert.equal(elements['confirm-dialog'].open,true); elements['confirm-dialog'].oncancel({preventDefault(){}}); await closePending; assert.notEqual(sent.at(-1).type,'close');
const reloadPending=elements.reset.onclick(); elements['confirm-accept'].onclick(); await reloadPending; assert.equal(sent.at(-1).type,'load');
receive({data:{type:'loaded',entries:save.entries}});
elements.search.value='没有'; elements.search.oninput(); assert.equal(elements.searchcount.textContent,'0 个应用');
elements.search.value=''; elements.search.oninput(); const deletePending=elements.delete.onclick(); assert.equal(elements['confirm-accept'].textContent,'删除快捷键'); assert.equal(elements['confirm-dialog'].open,true); elements['confirm-accept'].onclick(); await deletePending; assert.equal(sent.at(-1).entries.length,0); receive({data:{type:'saved'}}); assert.equal(elements.searchcount.textContent,'0 个应用');
console.log('PASS UI bridge: empty state, create, record, save failure/retry, dirty guard, search, reload, delete last item.');

receive({data:{type:'loaded',entries:Array.from({length:24},(_,i)=>({name:'应用'+i,processName:'app'+i,exePath:'app.exe',key:''}))}});
assert.equal(elements.list.children.length,6); assert.equal(elements.listpager.children[1].textContent,'1 / 4');
elements.listpager.children[2].onclick(); assert.equal(elements.listpager.children[1].textContent,'2 / 4');
elements.search.value='app23'; elements.search.oninput(); assert.equal(elements.list.children.length,1); assert.equal(elements.listpager.style.display,'none');
console.log('PASS pagination: 24 entries, six per page, next page, search resets pagination.');

elements.running.onclick(); assert.equal(elements['running-dialog'].open,true); assert.equal(sent.at(-1).type,'runningApps');
await receive({data:{type:'runningApps',apps:Array.from({length:7},(_,i)=>({id:i+1,name:'运行应用'+i,process:'proc'+i,path:'C:\\App\\app'+i+'.exe',title:'窗口'+i,error:i===1?'无法读取路径':null}))}});
assert.equal(elements['running-list'].children.length,7); assert.equal(elements['running-list'].children[1].disabled,true);
elements['running-search'].value='proc6'; elements['running-search'].oninput(); assert.equal(elements['running-list'].children.length,1);
elements['running-list'].children[0].onclick(); assert.equal(sent.at(-1).type,'pickRunningApp'); assert.equal(sent.at(-1).id,7);
await receive({data:{type:'picked',path:'C:\\App\\chosen.exe',process:'chosen',name:'已选择的应用'}});
assert.equal(elements.path.value,'C:\\App\\chosen.exe'); assert.equal(elements.process.value,'chosen'); assert.equal(elements['running-dialog'].open,false);
elements.running.onclick(); await receive({data:{type:'runningAppError',message:'应用已退出'}}); assert.equal(elements['running-status'].textContent,'应用已退出');
elements['running-close'].onclick(); assert.equal(elements['running-dialog'].open,false);
console.log('PASS running app picker: all entries without pagination, search, inaccessible entry, selection fills fields, stale app error, cancel.');
console.log("PASS styled confirmation: discard cancellation, Escape, discard acceptance, deletion acceptance.");
})().catch(error => { console.error(error); process.exitCode=1; });
