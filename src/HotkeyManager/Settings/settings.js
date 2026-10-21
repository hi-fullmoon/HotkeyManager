const $ = id => document.getElementById(id);
const send = message => window.chrome.webview.postMessage(message);
const pageSize = 6;
let page = 0;
const appMarks = { wechat: ['微','#e5f6ec','#29a25b'], weixin: ['微','#e5f6ec','#29a25b'], code: ['⌘','#e5f1fc','#2d90d4'], chrome: ['◉','#fff2df','#d39530'], explorer: ['▣','#fff5d9','#c59d34'], notepad: ['▤','#edf1f8','#7189aa'] };
let items = [], selected = -1, draft = '', recording = false, dirty = false, pending = null, busy = false;
function toast(message) {
  $('toast').textContent = message; $('toast').style.display = 'block';
  clearTimeout(window.toastTimer); window.toastTimer = setTimeout(() => $('toast').style.display = 'none', 3000);
}
function keys(element, key) {
  element.replaceChildren();
  if (!key) { element.textContent = '点击录制快捷键'; return; }
  key.split('+').forEach(value => { const k = document.createElement('kbd'); k.textContent = value; element.append(k); });
}
function stopRecording() {
  recording = false; $('record').classList.remove('recording'); keys($('record'), draft);
  $('recordhint').textContent = '点击上方区域，然后按下想使用的组合键';
  send({ type: 'record', active: false });
}
let confirmationPending = false;
function askConfirmation({ title, description, accept, cancel = '取消', kind = 'delete' }) {
  if (confirmationPending) return Promise.resolve(false);
  stopRecording();
  const dialog = $('confirm-dialog');
  $('confirm-title').textContent = title;
  $('confirm-description').textContent = description;
  $('confirm-accept').textContent = accept;
  $('confirm-cancel').textContent = cancel;
  dialog.dataset.kind = kind;
  confirmationPending = true;
  return new Promise(resolve => {
    let settled = false;
    const finish = accepted => {
      if (settled) return;
      settled = true; confirmationPending = false;
      dialog.close(); resolve(accepted);
    };
    $('confirm-accept').onclick = () => finish(true);
    $('confirm-cancel').onclick = () => finish(false);
    dialog.oncancel = event => { event.preventDefault(); finish(false); };
    dialog.onclose = () => finish(false);
    dialog.showModal();
    $('confirm-cancel').focus();
  });
}
async function discard() {
  return !dirty || await askConfirmation({ title: '放弃未保存的更改？', description: '当前修改尚未保存。放弃后，这些修改将不会生效。', accept: '放弃更改', cancel: '继续编辑', kind: 'discard' });
}
function render() {
  $('list').replaceChildren();
  const query = $('search').value.trim().toLowerCase();
  const matches = items.map((a,index) => ({a,index})).filter(({a}) =>
    ((a.name || a.processName || '新应用') + ' ' + (a.processName || '') + ' ' + (a.key || '')).toLowerCase().includes(query));
  const count = matches.length;
  const pageCount = Math.max(1, Math.ceil(count / pageSize));
  page = Math.max(0, Math.min(page, pageCount - 1));
  matches.slice(page * pageSize, (page + 1) * pageSize).forEach(({a,index}) => {
    const name = a.name || a.processName || '新应用';
    const button = document.createElement('button'); button.className = 'item' + (index === selected ? ' selected' : '');
    button.disabled = busy;
    const icon = document.createElement('span'); icon.className = 'icon'; const mark = appMarks[(a.processName || '').toLowerCase()] || [name[0].toUpperCase(),'#eaf1ff','#326be8']; icon.style.setProperty('--bg', mark[1]); icon.style.setProperty('--fg', mark[2]); icon.textContent = mark[0];
    const text = document.createElement('div'), title = document.createElement('div'), sub = document.createElement('div');
    title.className = 'name'; title.textContent = name; sub.className = 'sub'; sub.textContent = a.processName ? a.processName + '.exe' : '等待配置应用'; text.append(title, sub);
    const shortcut = document.createElement('span'); shortcut.className = 'keys'; keys(shortcut, a.key || ''); if (!a.key) shortcut.textContent = '待设置';
    button.append(icon, text, shortcut);
    button.onclick = async () => { if (selected !== index && await discard()) { if (selected >= 0 && items[selected]._new) { items.splice(selected, 1); if (index > selected) index--; } load(index); } };
    $('list').append(button);
  });
  $('listpager').replaceChildren();
  $('listpager').style.display = pageCount > 1 ? 'flex' : 'none';
  if (pageCount > 1) {
    const previous = document.createElement('button'), next = document.createElement('button'), label = document.createElement('span');
    previous.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 6l-6 6 6 6"/></svg>'; previous.ariaLabel = '上一页'; previous.title = '上一页'; previous.disabled = busy || page === 0;
    next.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 6l6 6-6 6"/></svg>'; next.ariaLabel = '下一页'; next.title = '下一页'; next.disabled = busy || page === pageCount - 1;
    label.textContent = (page + 1) + ' / ' + pageCount;
    previous.onclick = () => { page--; render(); }; next.onclick = () => { page++; render(); };
    $('listpager').append(previous, label, next);
  }
  $('searchcount').textContent = count + ' 个应用';
  if (!count) { const empty = document.createElement('p'); empty.className = 'empty-state'; empty.textContent = items.length ? '没有匹配的快捷键' : '还没有快捷键，点击右上角新建'; $('list').append(empty); }
}
function load(index) {
  stopRecording(); selected = index; dirty = false; const a = items[index];
  $('name').value = a?.name || a?.processName || ''; $('path').value = a?.exePath || ''; $('process').value = a?.processName || ''; draft = a?.key || '';
  keys($('record'), draft); $('error').textContent = '';
  document.querySelectorAll('.form input,.form button').forEach(el => el.disabled = !a || busy);
  const matchingIndices = items.map((a,i) => ({a,i})).filter(({a}) => ((a.name || a.processName || '新应用') + ' ' + (a.processName || '') + ' ' + (a.key || '')).toLowerCase().includes($('search').value.trim().toLowerCase()));
  const position = matchingIndices.findIndex(({i}) => i === index);
  if (position >= 0) page = Math.floor(position / pageSize);
  render();
}
function setBusy(value) {
  busy = value; $('add').disabled = value; $('reset').disabled = value;
  document.querySelectorAll('.form input,.form button').forEach(el => el.disabled = value || selected < 0); render();
}
function saveEntries(entries, index) {
  stopRecording(); pending = { entries, index }; setBusy(true); send({ type: 'save', entries });
}
for (const id of ['name', 'path', 'process']) $(id).oninput = () => dirty = true;
$('search').oninput = () => { page = 0; render(); };
$('record').onclick = () => {
  if (recording) { stopRecording(); return; }
  recording = true; $('error').textContent = ''; $('record').classList.add('recording'); $('record').textContent = '请按下组合键…';
  $('recordhint').textContent = '按 Esc 取消录制'; send({ type: 'record', active: true });
};
const special = { ' ': 'Space', ArrowUp: 'Up', ArrowDown: 'Down', ArrowLeft: 'Left', ArrowRight: 'Right', PageUp: 'PageUp', PageDown: 'PageDown', PrintScreen: 'PrintScreen', '+': 'Oemplus', '-': 'OemMinus', ',': 'Oemcomma', '.': 'OemPeriod', '/': 'OemQuestion', ';': 'OemSemicolon', "'": 'OemQuotes', '[': 'OemOpenBrackets', ']': 'OemCloseBrackets', '\\': 'OemPipe', '`': 'Oemtilde' };
document.addEventListener('keydown', e => {
  if (!recording) return;
  e.preventDefault(); e.stopPropagation();
  if (e.key === 'Escape') { stopRecording(); return; }
  if (['Control', 'Alt', 'Shift', 'Meta'].includes(e.key)) return;
  let key = e.code.startsWith('Numpad') ? ({ NumpadAdd:'Add', NumpadSubtract:'Subtract', NumpadMultiply:'Multiply', NumpadDivide:'Divide', NumpadDecimal:'Decimal', NumpadEnter:'Enter' }[e.code] || e.code.replace('Numpad','NumPad')) : e.code.startsWith('Key') ? e.code.slice(3) : e.code.startsWith('Digit') ? e.code.slice(5) : special[e.key] || e.key;
  if (!e.ctrlKey && !e.altKey && !e.shiftKey && !e.metaKey && !/^F([1-9]|1[0-9]|2[0-4])$/.test(key)) { $('error').textContent = '请搭配 Ctrl、Alt、Shift 或 Win 使用。'; return; }
  draft = [e.ctrlKey ? 'Ctrl' : '', e.altKey ? 'Alt' : '', e.shiftKey ? 'Shift' : '', e.metaKey ? 'Win' : '', key].filter(Boolean).join('+');
  dirty = true; stopRecording();
}, true);
$('save').onclick = () => {
  const name = $('name').value.trim(), exePath = $('path').value.trim(), processName = $('process').value.trim().replace(/\.exe$/i, '');
  if (!name || !exePath || !processName || !draft) { $('error').textContent = '请填写应用信息，并录制快捷键。'; return; }
  const entries = items.map(a => { const copy = { ...a }; delete copy._new; return copy; });
  entries[selected] = { ...entries[selected], name, exePath, processName, key: draft }; saveEntries(entries, selected);
};
$('add').onclick = async () => {
  if (!await discard()) return;
  if (selected >= 0 && items[selected]._new) items.splice(selected, 1);
  items.push({ name: '新应用', key: '', processName: '', exePath: '', windowClass: '', _new: true });
  $('search').value = ''; load(items.length - 1); dirty = true; $('name').focus(); $('name').select();
};
$('delete').onclick = async () => {
  if (selected < 0 || !await askConfirmation({ title: '删除这条快捷键？', description: '将移除「' + (items[selected].name || items[selected].processName || '新应用') + '」的快捷键配置，不会卸载应用。', accept: '删除快捷键' })) return;
  const entries = items.filter((_, i) => i !== selected);
  if (items[selected]._new) { items = entries; load(Math.min(selected, items.length - 1)); return; }
  saveEntries(entries, Math.min(selected, entries.length - 1));
};
$('browse').onclick = () => { stopRecording(); send({ type: 'browse' }); };
let runningApps = [], runningBusy = false;
function renderRunningApps() {
  const query = $('running-search').value.trim().toLowerCase();
  const apps = runningApps.filter(app => [app.name, app.title, app.process].join(' ').toLowerCase().includes(query));
  $('running-list').replaceChildren();
  for (const app of apps) {
    const row = document.createElement('button'); row.type = 'button'; row.className = 'running-item'; row.disabled = runningBusy || !!app.error;
    row.title = app.error || app.path;
    const icon = document.createElement('span'); icon.className = 'icon'; icon.style.setProperty('--bg','#eaf1ff'); icon.style.setProperty('--fg','#3d68e5'); icon.textContent = (app.name || app.process)[0].toUpperCase();
    const text = document.createElement('div'); text.className = 'running-text';
    const name = document.createElement('div'); name.className = 'running-name'; name.textContent = app.name;
    const meta = document.createElement('div'); meta.className = 'running-meta'; meta.textContent = app.error || app.process + '.exe · ' + app.title;
    text.append(name, meta); row.append(icon, text);
    row.onclick = () => { runningBusy = true; renderRunningApps(); $('running-status').textContent = '正在读取应用信息…'; send({type:'pickRunningApp', id:app.id}); };
    $('running-list').append(row);
  }
  if (!apps.length) {
    const empty=document.createElement('p'); empty.className='running-empty';
    empty.textContent=runningBusy ? '正在查找应用…' : '未找到匹配的应用'; $('running-list').append(empty);
  }
  $('running-refresh').disabled=runningBusy;
  $('running-status').title='';
  $('running-status').textContent = runningBusy ? '正在读取应用列表…' : apps.length ? apps.length + ' 个应用 · 选择后自动填写' : '未找到应用，可尝试刷新或手动选择 exe';
}
function refreshRunningApps() {
  runningBusy=true; runningApps=[]; renderRunningApps(); send({type:'runningApps'});
}
$('running').onclick=()=>{stopRecording();$('running-search').value='';$('running-dialog').showModal();refreshRunningApps();$('running-search').focus();};
$('running-close').onclick=()=>{$('running-dialog').close();};
$('running-refresh').onclick=refreshRunningApps;
$('running-search').oninput=()=>{renderRunningApps();$('running-list').scrollTop=0;};
$('reset').onclick = async () => { if (await discard()) { stopRecording(); setBusy(true); send({ type: 'load' }); } };
window.chrome.webview.addEventListener('message', async event => {
  const m = event.data;
  switch (m.type) {
    case 'loaded': items = m.entries; setBusy(false); load(items.length ? 0 : -1); break;
    case 'saved': items = pending.entries; const index = pending.index; pending = null; dirty = false; setBusy(false); load(index); toast('配置已保存，快捷键已更新'); break;
    case 'error': pending = null; setBusy(false); $('error').textContent = m.message; toast(m.message); break;
    case 'picked': if ($('running-dialog').open) $('running-dialog').close(); runningBusy=false; $('path').value = m.path; $('process').value = m.process; if (!$('name').value || $('name').value === '新应用') $('name').value = m.name || m.process; dirty = true; $('error').textContent=''; break;
    case 'runningApps': if (!$('running-dialog').open) break; runningApps=m.apps; runningBusy=false; renderRunningApps(); break;
    case 'runningAppError': if (!$('running-dialog').open) break; runningBusy=false; renderRunningApps(); $('running-status').textContent=m.message; $('running-status').title=m.message; break;
    case 'stopRecording': stopRecording(); break;
    case 'closing': if ($('running-dialog').open) { $('running-dialog').close(); break; } if (await discard()) { stopRecording(); send({ type: 'close' }); } break;
  }
});
setBusy(true); send({ type: 'load' });
