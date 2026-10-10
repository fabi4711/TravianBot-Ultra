// Generic renderer for the headless WPF element tree sent by the server (see UiSnapshot.cs).
// Elements are cached by server id so DOM nodes, focus and scroll positions survive updates.
'use strict';

(() => {
  const mainHost = document.getElementById('main');
  const dialogHost = document.getElementById('dialogs');
  const messageHost = document.getElementById('messages');
  const statusEl = document.getElementById('status');
  const cache = new Map();
  let seen = new Set();
  let socket = null;
  let reconnectDelay = 500;

  // ---- transport ---------------------------------------------------------------------------

  function connect() {
    const scheme = location.protocol === 'https:' ? 'wss' : 'ws';
    socket = new WebSocket(`${scheme}://${location.host}/ws`);
    socket.onopen = () => {
      reconnectDelay = 500;
      statusEl.classList.add('ok');
      statusEl.textContent = 'Connected';
    };
    socket.onclose = () => {
      statusEl.classList.remove('ok');
      statusEl.textContent = 'Disconnected – reconnecting…';
      setTimeout(connect, reconnectDelay);
      reconnectDelay = Math.min(reconnectDelay * 2, 8000);
    };
    socket.onmessage = event => {
      const data = JSON.parse(event.data);
      if (data.download !== undefined) {
        const link = document.createElement('a');
        link.href = data.download;
        link.download = '';
        document.body.append(link);
        link.click();
        link.remove();
        return;
      }
      if (data.open !== undefined) {
        window.open(data.open, '_blank', 'noopener');
        return;
      }
      if (data.clip !== undefined) {
        navigator.clipboard?.writeText(data.clip).catch(() => {});
        return;
      }
      renderSnapshot(data);
    };
  }

  function send(message) {
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(JSON.stringify(message));
    }
  }

  // ---- snapshot ----------------------------------------------------------------------------

  function renderSnapshot(snapshot) {
    seen = new Set();
    const windows = snapshot.windows || [];
    const main = windows.find(w => w.main) || windows[0];
    if (main) {
      document.title = main.title || 'Tbot Ultra';
      const root = render(main.root);
      setChildren(mainHost, root ? [root] : []);
      if (main.bg) mainHost.style.background = main.bg;
    }

    const dialogs = windows.filter(w => w !== main).map(renderWindow);
    setChildren(dialogHost, dialogs);
    renderMessages(snapshot.messages || []);
    renderFileRequests(snapshot.files || []);

    for (const id of [...cache.keys()]) {
      if (!seen.has(id)) cache.delete(id);
    }
  }

  function renderWindow(win) {
    const key = 'win' + win.i;
    let frame = cache.get(key)?.el;
    if (!frame) {
      frame = el('div', 'dialog-backdrop');
      const box = el('div', 'dialog');
      const bar = el('div', 'dialog-title');
      const title = el('span');
      const close = el('button', 'dialog-close');
      close.textContent = '✕';
      close.title = 'Close';
      close.onclick = () => send({ e: 'close', i: win.i });
      bar.append(title, close);
      const body = el('div', 'dialog-body');
      box.append(bar, body);
      frame.append(box);
      frame._title = title;
      frame._body = body;
      frame._box = box;
    }
    cache.set(key, { el: frame, k: 'win' });
    seen.add(key);
    frame.classList.toggle('modeless', !win.modal);
    frame._title.textContent = win.title || '';
    frame._box.style.width = win.w ? `min(${win.w}px, 96vw)` : '';
    frame._box.style.height = win.h ? `min(${win.h}px, 92vh)` : '';
    if (win.bg) frame._box.style.background = win.bg;
    const root = render(win.root);
    setChildren(frame._body, root ? [root] : []);
    return frame;
  }

  function renderMessages(messages) {
    const nodes = messages.map(message => {
      const key = 'msg' + message.id;
      let box = cache.get(key)?.el;
      if (!box) {
        box = el('div', 'dialog-backdrop message');
        const dialog = el('div', 'dialog small');
        const title = el('div', 'dialog-title');
        title.textContent = message.caption || 'Tbot Ultra';
        const text = el('div', 'message-text');
        text.textContent = message.text;
        const buttons = el('div', 'message-buttons');
        const choices = {
          OK: ['OK'], OKCancel: ['OK', 'Cancel'], YesNo: ['Yes', 'No'], YesNoCancel: ['Yes', 'No', 'Cancel'],
        }[message.buttons] || ['OK'];
        for (const choice of choices) {
          const button = el('button', 'btn' + (choice === choices[0] ? ' primary' : ''));
          button.textContent = choice;
          button.onclick = () => send({ e: 'msg', id: message.id, r: choice });
          buttons.append(button);
        }
        dialog.append(title, text, buttons);
        box.append(dialog);
        box.classList.add('icon-' + (message.icon || 'None').toLowerCase());
      }
      cache.set(key, { el: box, k: 'msg' });
      seen.add(key);
      return box;
    });
    setChildren(messageHost, nodes);
  }

  function renderFileRequests(requests) {
    let host = document.getElementById('files');
    if (!host) {
      host = el('div');
      host.id = 'files';
      document.body.append(host);
    }
    const nodes = requests.map(request => {
      const key = 'file' + request.id;
      let box = cache.get(key)?.el;
      if (!box) {
        box = el('div', 'dialog-backdrop message');
        const dialog = el('div', 'dialog small');
        const title = el('div', 'dialog-title');
        title.textContent = request.title;
        const body = el('div', 'message-text');
        const input = el('input');
        input.type = 'file';
        if (request.accept) input.accept = request.accept;
        body.append(input);
        const buttons = el('div', 'message-buttons');
        const upload = el('button', 'btn primary');
        upload.textContent = 'Upload';
        upload.onclick = async () => {
          if (!input.files.length) return;
          const form = new FormData();
          form.append('file', input.files[0]);
          upload.disabled = true;
          await fetch(`api/upload/${request.id}`, { method: 'POST', body: form });
        };
        const cancel = el('button', 'btn');
        cancel.textContent = 'Cancel';
        cancel.onclick = () => send({ e: 'filecancel', id: request.id });
        buttons.append(upload, cancel);
        dialog.append(title, body, buttons);
        box.append(dialog);
      }
      cache.set(key, { el: box, k: 'file' });
      seen.add(key);
      return box;
    });
    setChildren(host, nodes);
  }

  // ---- element rendering -------------------------------------------------------------------

  function render(node) {
    if (!node) return null;
    if (node.k === 'text') {
      const span = el('span', 'txt');
      span.textContent = node.t ?? '';
      return span;
    }
    const kind = node.k + (node.multi ? 'm' : '') + (node.edit ? 'e' : '');
    let entry = cache.get(node.i);
    if (!entry || entry.k !== kind) {
      entry = { el: create(node), k: kind };
      cache.set(node.i, entry);
    }
    seen.add(node.i);
    const element = entry.el;
    common(element, node);
    update(element, node);
    return element;
  }

  function create(node) {
    switch (node.k) {
      case 'tb': return el('div', 'tb');
      case 'button': {
        const button = el('button', 'btn');
        button.onclick = event => { event.stopPropagation(); send({ e: 'click', i: node.i }); };
        return button;
      }
      case 'toggle': {
        const button = el('button', 'btn toggle');
        button.onclick = event => { event.stopPropagation(); send({ e: 'check', i: node.i }); };
        return button;
      }
      case 'check':
      case 'radio': {
        const label = el('label', 'chk');
        const input = el('input');
        input.type = node.k === 'radio' ? 'radio' : 'checkbox';
        input.onclick = event => { event.preventDefault(); send({ e: 'check', i: node.i }); };
        const content = el('span', 'chk-label');
        label.append(input, content);
        label._input = input;
        label._content = content;
        return label;
      }
      case 'input': {
        const input = node.multi ? el('textarea', 'input') : el('input', 'input');
        bindText(input, node.i);
        return input;
      }
      case 'password': {
        const input = el('input', 'input');
        input.type = 'password';
        input.autocomplete = 'new-password';
        input.onchange = () => send({ e: 'text', i: node.i, v: input.value });
        return input;
      }
      case 'combo': {
        if (node.edit) {
          const wrapper = el('span', 'combo-edit');
          const input = el('input', 'input');
          const list = el('datalist');
          list.id = 'dl' + node.i;
          input.setAttribute('list', list.id);
          bindText(input, node.i);
          wrapper.append(input, list);
          wrapper._input = input;
          wrapper._list = list;
          return wrapper;
        }
        const select = el('select', 'input');
        select.onchange = () => send({ e: 'select', i: node.i, v: select.selectedIndex });
        return select;
      }
      case 'tabs': {
        const tabs = el('div', 'tabs');
        tabs._bar = el('div', 'tabbar');
        tabs._body = el('div', 'tabbody');
        tabs.append(tabs._bar, tabs._body);
        return tabs;
      }
      case 'table': {
        const wrap = el('div', 'table-wrap');
        const table = el('table', 'grid-table');
        wrap._head = el('thead');
        wrap._body = el('tbody');
        table.append(wrap._head, wrap._body);
        wrap.append(table);
        wrap._table = table;
        return wrap;
      }
      case 'list': return el('div', 'list');
      case 'items': return el('div', 'items');
      case 'expander': {
        const box = el('div', 'expander');
        box._head = el('div', 'expander-head');
        box._arrow = el('span', 'expander-arrow');
        box._title = el('div', 'expander-title');
        box._head.append(box._arrow, box._title);
        // An element header holds its own controls (toggles, buttons); only the arrow expands it then.
        box._head.onclick = (event) => {
          if (box._elementHeader && !box._arrow.contains(event.target)) return;
          send({ e: 'expand', i: node.i });
        };
        box._body = el('div', 'expander-body');
        box.append(box._head, box._body);
        return box;
      }
      case 'progress': {
        const bar = el('div', 'progress');
        bar._fill = el('div', 'progress-fill');
        bar.append(bar._fill);
        return bar;
      }
      case 'sep': return el('hr', 'sep');
      case 'img': return el('img', 'img');
      case 'shape': return el('div', 'shape');
      case 'none': return el('span', 'none');
      default: return el('div', node.k);
    }
  }

  function bindText(input, id) {
    let timer = null;
    let last = null;
    const flush = () => {
      clearTimeout(timer);
      if (input.value !== last) {
        last = input.value;
        send({ e: 'text', i: id, v: input.value });
      }
    };
    input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(flush, 350); });
    input.addEventListener('blur', () => { flush(); send({ e: 'blur', i: id }); });
    input.addEventListener('keydown', event => {
      if (event.key === 'Enter' && input.tagName !== 'TEXTAREA') {
        flush();
        send({ e: 'key', i: id, k: 'Enter' });
      }
    });
    input._sync = value => {
      last = value;
      if (document.activeElement !== input && input.value !== value) input.value = value;
    };
  }

  function update(element, node) {
    switch (node.k) {
      case 'tb':
        if (node.inl) {
          element.replaceChildren(...node.inl.map(inline => {
            if (inline.br) return el('br');
            const span = el(inline.href ? 'a' : 'span');
            span.textContent = glyphs(inline.t ?? '');
            if (inline.fg) span.style.color = inline.fg;
            if (inline.fw) span.style.fontWeight = inline.fw;
            if (inline.href) { span.href = inline.href; span.target = '_blank'; span.rel = 'noopener'; }
            return span;
          }));
        } else if (element.textContent !== glyphs(node.t ?? '')) {
          element.textContent = glyphs(node.t ?? '');
        }
        element.classList.toggle('wrap', !!node.wrap);
        element.style.textAlign = node.ta || '';
        break;
      case 'button':
      case 'toggle':
        content(element, node);
        element.disabled = !!node.off;
        element.classList.toggle('on', node.chk === true);
        element.dataset.style = node.style || '';
        // A content-less toggle is a switch in the desktop theme.
        element.classList.toggle('switch', node.k === 'toggle' && !node.t && !(node.c && node.c.length));
        break;
      case 'check':
      case 'radio':
        element._input.checked = node.chk === true;
        element._input.indeterminate = node.chk === null;
        element._input.disabled = !!node.off;
        content(element._content, node);
        break;
      case 'input':
        element._sync(node.val ?? '');
        element.readOnly = !!node.ro;
        element.disabled = !!node.off;
        break;
      case 'password':
        element.placeholder = node.set ? '••••••••' : '';
        element.disabled = !!node.off;
        break;
      case 'combo':
        if (node.edit) {
          element._input._sync(node.val ?? '');
          syncOptions(element._list, node.opts, -1);
          element._input.disabled = !!node.off;
        } else {
          syncOptions(element, node.opts, node.sel);
          element.disabled = !!node.off;
        }
        break;
      case 'tabs':
        renderTabs(element, node);
        break;
      case 'table':
        renderTable(element, node);
        break;
      case 'list':
        renderList(element, node);
        break;
      case 'items':
        element.dataset.o = node.o || 'v';
        setChildren(element, (node.c || []).map(render).filter(Boolean));
        break;
      case 'expander':
        element._arrow.textContent = node.open ? '▾ ' : '▸ ';
        element._elementHeader = !!node.h;
        if (node.h) setChildren(element._title, [render(node.h)].filter(Boolean));
        else element._title.textContent = node.hdr || '';
        setChildren(element._body, node.c ? [render(node.c)].filter(Boolean) : []);
        break;
      case 'progress': {
        const range = (node.max ?? 100) - (node.min ?? 0);
        const percent = range > 0 ? ((node.val - (node.min ?? 0)) / range) * 100 : 0;
        element._fill.style.width = node.ind ? '100%' : `${Math.max(0, Math.min(100, percent))}%`;
        element.classList.toggle('indeterminate', !!node.ind);
        if (node.fg) element._fill.style.background = node.fg;
        break;
      }
      case 'img':
        if (node.src && element.getAttribute('src') !== node.src) element.src = node.src;
        break;
      case 'shape':
        element.classList.toggle('round', !!node.round);
        element.style.background = node.bg || '';
        element.style.borderColor = node.bb || 'transparent';
        break;
      case 'grid':
        element.style.gridTemplateColumns = (node.cols && node.cols.length) ? node.cols.join(' ') : 'minmax(0,1fr)';
        element.style.gridTemplateRows = (node.rows && node.rows.length) ? node.rows.join(' ') : '';
        setChildren(element, (node.c || []).map(render).filter(Boolean));
        break;
      case 'stack':
      case 'wrap':
        element.dataset.o = node.o || 'v';
        setChildren(element, (node.c || []).map(render).filter(Boolean));
        break;
      case 'ugrid':
        element.style.gridTemplateColumns = `repeat(${node.n || 1}, minmax(0,1fr))`;
        setChildren(element, (node.c || []).map(render).filter(Boolean));
        break;
      case 'dock':
        renderDock(element, node);
        break;
      case 'canvas':
        setChildren(element, (node.c || []).map(child => {
          const rendered = render(child);
          if (rendered) {
            rendered.style.position = 'absolute';
            rendered.style.left = (child.x ?? 0) + 'px';
            rendered.style.top = (child.y ?? 0) + 'px';
          }
          return rendered;
        }).filter(Boolean));
        break;
      case 'box':
      case 'border':
      case 'scroll':
        if (node.hdr) element.dataset.hdr = node.hdr;
        setChildren(element, (node.c || []).map(render).filter(Boolean));
        break;
    }
  }

  function common(element, node) {
    const style = element.style;
    style.margin = box(node.m);
    style.padding = box(node.pad);
    style.width = node.w != null ? node.w + 'px' : '';
    style.height = node.h != null ? node.h + 'px' : '';
    style.minWidth = node.minw != null ? node.minw + 'px' : '';
    style.maxWidth = node.maxw != null ? node.maxw + 'px' : '';
    style.minHeight = node.minh != null ? node.minh + 'px' : '';
    style.maxHeight = node.maxh != null ? node.maxh + 'px' : '';
    style.visibility = node.hidden ? 'hidden' : '';
    style.opacity = node.op != null ? node.op : '';
    if (node.gr != null) {
      style.gridRow = `${node.gr + 1} / span ${node.grs || 1}`;
      style.gridColumn = `${node.gc + 1} / span ${node.gcs || 1}`;
    } else {
      style.gridRow = '';
      style.gridColumn = '';
    }
    element.dataset.ha = node.ha || '';
    element.dataset.va = node.va || '';
    if (node.k !== 'shape') {
      style.color = node.fg || '';
      style.background = node.bg || '';
      if (node.k === 'border') {
        style.borderColor = node.bb || 'transparent';
        style.borderWidth = node.bt ? box(node.bt) : '0';
        style.borderRadius = node.cr ? node.cr + 'px' : '';
      } else if (node.bb && (node.k === 'button' || node.k === 'input')) {
        style.borderColor = node.bb;
      }
    }
    style.fontWeight = node.fw || '';
    style.fontSize = node.fs ? node.fs + 'px' : '';
    style.fontStyle = node.it ? 'italic' : '';
    element.classList.toggle('disabled', !!node.off);
    if (node.tip) element.title = node.tip; else element.removeAttribute('title');
    if (node.n) element.dataset.name = node.n;
  }

  function content(element, node) {
    if (node.c && node.c.length) {
      setChildren(element, node.c.map(render).filter(Boolean));
    } else if (element.textContent !== glyphs(node.t ?? '') || element.children.length) {
      element.textContent = glyphs(node.t ?? '');
    }
  }

  // WPF DockPanel: each child takes a side of the remaining space; the last one fills it.
  function renderDock(element, node) {
    const items = (node.c || []).map(child => ({ el: render(child), dock: child.dock || 'left' })).filter(item => item.el);
    const build = (start) => {
      if (start >= items.length) return [];
      const item = items[start];
      const isLast = start === items.length - 1;
      if (isLast && node.fill !== false) {
        item.el.style.flex = '1 1 auto';
        return [item.el];
      }
      item.el.style.flex = '0 0 auto';
      const horizontal = item.dock === 'left' || item.dock === 'right';
      const rest = build(start + 1);
      const restBox = el('div', 'dock-rest');
      restBox.style.display = 'flex';
      restBox.style.flexDirection = horizontal ? 'row' : 'column';
      restBox.style.flex = '1 1 auto';
      restBox.style.minWidth = '0';
      restBox.style.minHeight = '0';
      restBox.append(...rest);
      if (node.fill === false && rest.length && isLastNonFill(start + 1)) restBox.style.justifyContent = 'flex-start';
      const pair = el('div', 'dock-pair');
      pair.style.display = 'flex';
      pair.style.flexDirection = horizontal ? 'row' : 'column';
      pair.style.flex = '1 1 auto';
      pair.style.minWidth = '0';
      pair.style.minHeight = '0';
      pair.style.alignItems = horizontal ? 'center' : 'stretch';
      if (item.dock === 'left' || item.dock === 'top') pair.append(item.el, restBox);
      else pair.append(restBox, item.el);
      return [pair];
    };
    const isLastNonFill = () => true;
    element.style.display = 'flex';
    element.replaceChildren(...build(0));
  }

  function renderTabs(element, node) {
    const buttons = (node.tabs || []).map(tab => {
      const key = 'tab' + tab.i;
      let button = cache.get(key)?.el;
      if (!button) {
        button = el('button', 'tab');
        button.onclick = () => send({ e: 'select', i: node.i, v: tab.i });
      }
      cache.set(key, { el: button, k: 'tab' });
      seen.add(key);
      button.textContent = glyphs(tab.t) || '·';
      button.disabled = !!tab.off;
      button.classList.toggle('active', tab.i === node.sel);
      button.style.color = tab.fg || '';
      return button;
    });
    setChildren(element._bar, buttons);
    element._bar.style.display = buttons.length > 1 && !node.nobar ? '' : 'none';
    const body = render(node.c);
    setChildren(element._body, body ? [body] : []);
  }

  function renderTable(element, node) {
    const headKey = JSON.stringify(node.cols);
    if (element._headKey !== headKey) {
      element._headKey = headKey;
      const row = el('tr');
      for (const column of node.cols || []) {
        const th = el('th');
        th.textContent = column.h || '';
        if (column.w) th.style.width = column.w + 'px';
        row.append(th);
      }
      element._head.replaceChildren(row);
    }

    const selected = new Set(node.sel || []);
    const rows = (node.rows || []).map((row, index) => {
      const tr = el('tr');
      tr.classList.toggle('selected', selected.has(index));
      tr.onclick = event => selectRow(node, index, event);
      tr.ondblclick = () => send({ e: 'dblclick', i: node.i });
      row.c.forEach((cell, columnIndex) => {
        const td = el('td');
        if (cell == null || typeof cell === 'string') {
          td.textContent = cell ?? '';
        } else if (cell.k === 'cellcheck') {
          const box = el('input');
          box.type = 'checkbox';
          box.checked = cell.chk === true;
          box.disabled = !!cell.ro;
          box.onclick = event => { event.preventDefault(); event.stopPropagation(); send({ e: 'cellcheck', i: node.i, r: index, c: columnIndex }); };
          td.append(box);
        } else {
          const rendered = render(cell);
          if (rendered) td.append(rendered);
        }
        tr.append(td);
      });
      return tr;
    });
    element._body.replaceChildren(...rows);
    element.dataset.more = node.total > rows.length ? `${node.total - rows.length} more rows` : '';
  }

  function selectRow(node, index, event) {
    let selection = [index];
    if (node.multi && (event.ctrlKey || event.metaKey)) {
      const current = new Set(node.sel || []);
      if (current.has(index)) current.delete(index); else current.add(index);
      selection = [...current];
    } else if (node.multi && event.shiftKey && (node.sel || []).length) {
      const anchor = node.sel[0];
      selection = [];
      for (let i = Math.min(anchor, index); i <= Math.max(anchor, index); i++) selection.push(i);
    }
    send({ e: 'rows', i: node.i, v: selection });
  }

  function renderList(element, node) {
    const selected = new Set(node.sel || []);
    const rows = (node.items || []).map((item, index) => {
      const key = `li${node.i}:${item && item.i != null ? item.i : index}`;
      let row = cache.get(key)?.el;
      if (!row) row = el('div', 'list-item');
      cache.set(key, { el: row, k: 'li' });
      seen.add(key);
      row.onclick = event => selectRow(node, index, event);
      row.ondblclick = () => send({ e: 'dblclick', i: node.i });
      row.classList.toggle('selected', selected.has(index));
      const rendered = render(item);
      setChildren(row, rendered ? [rendered] : []);
      return row;
    });
    setChildren(element, rows);
  }

  function syncOptions(select, options, selectedIndex) {
    const key = JSON.stringify(options || []);
    if (select._key !== key) {
      select._key = key;
      select.replaceChildren(...(options || []).map(text => {
        const option = el('option');
        option.textContent = text;
        option.value = text;
        return option;
      }));
    }
    if (select.tagName === 'SELECT' && select.selectedIndex !== selectedIndex) {
      select.selectedIndex = selectedIndex;
    }
  }

  // ---- helpers -----------------------------------------------------------------------------

  // Segoe MDL2 / Fluent icon code points used by the desktop UI, mapped to portable symbols.
  const GLYPHS = {
    0xE713: '⚙', 0xE8FD: '☰', 0xE77B: '👤', 0xE715: '✉', 0xE916: '⏱', 0xE90F: '🔧', 0xE8F1: '📚',
    0xE825: '🏦', 0xE80F: '⌂', 0xE7C3: '📄', 0xE7C1: '⚑', 0xE768: '▶', 0xE719: '🛒', 0xE716: '👥',
    0xE710: '+', 0xE70E: '˄', 0xE70D: '˅', 0xE708: '☾', 0xE707: '📍', 0xE72C: '⟳', 0xE711: '✕',
    0xE74D: '🗑', 0xE73E: '✓', 0xE946: 'ℹ', 0xE7BA: '⚠', 0xE721: '🔍', 0xE8A7: '⧉', 0xE896: '⬇',
    0xE898: '⬆', 0xE769: '⏸', 0xE71A: '⏹', 0xE9F5: '⚡', 0xE945: '⚡', 0xE7E8: '⏻', 0xE8C8: '⧉',
  };

  function glyphs(text) {
    if (!text) return text;
    let out = '';
    for (const ch of text) {
      const code = ch.codePointAt(0);
      if (code >= 0xE000 && code <= 0xF8FF) out += GLYPHS[code] ?? '';
      else out += ch;
    }
    return out;
  }

  function setChildren(parent, children) {
    const current = parent.childNodes;
    let same = current.length === children.length;
    for (let i = 0; same && i < children.length; i++) {
      if (current[i] !== children[i]) same = false;
    }
    if (!same) parent.replaceChildren(...children);
  }

  function box(values) {
    if (!values) return '';
    const [left, top, right, bottom] = values;
    return `${top}px ${right}px ${bottom}px ${left}px`;
  }

  function el(tag, className) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    return element;
  }

  // Live view of the bot's browser (noVNC), served next to the web UI.
  fetch('api/info').then(response => response.json()).then(info => {
    if (!info.vncUrl) return;
    const link = document.createElement('a');
    link.className = 'vnc-link';
    link.textContent = '🖥 Browser';
    link.title = "Live view of the bot's browser";
    link.target = '_blank';
    link.rel = 'noopener';
    link.href = info.vncUrl.replace('{host}', location.hostname);
    document.body.append(link);
  }).catch(() => {});

  connect();
})();
