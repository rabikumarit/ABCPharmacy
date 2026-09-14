const $ = selector => document.querySelector(selector);
const medicinesBody = $('#medicines');
let searchTimer;

function formatMoney(value) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(value); }
// API values may be either yyyy-MM-dd or a full ISO date-time. Parse only the calendar part.
function calendarDate(value) {
    const [year, month, day] = String(value).slice(0, 10).split('-').map(Number);
    return new Date(year, month - 1, day);
}
function formatDate(value) { return calendarDate(value).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }); }
function expirySoon(date) { return calendarDate(date) < new Date(Date.now() + 30 * 86400000); }
function escapeHtml(value) { const node = document.createElement('div'); node.textContent = value; return node.innerHTML; }

async function loadMedicines(search = '') {
    const response = await fetch('/api/medicines?search=' + encodeURIComponent(search));
    const medicines = await response.json();
    $('#count').textContent = `${medicines.length} medicine${medicines.length === 1 ? '' : 's'}`;
    medicinesBody.innerHTML = medicines.length ? medicines.map(m => {
        const classes = [expirySoon(m.expiryDate) ? 'expiring' : '', m.quantity < 10 ? 'low-stock' : ''].join(' ');
        return `<tr class="${classes}"><td><strong>${escapeHtml(m.name)}</strong></td><td>${escapeHtml(m.brand)}</td><td>${formatDate(m.expiryDate)}</td><td>${m.quantity}</td><td>${formatMoney(m.price)}</td><td><button class="sell" data-id="${m.id}" data-name="${escapeHtml(m.name)}" data-stock="${m.quantity}">Record sale</button></td></tr>`;
    }).join('') : '<tr><td colspan="6" class="empty">No medicines match your search.</td></tr>';
}

async function loadSales() {
    const response = await fetch('/api/sales'); const sales = await response.json();
    $('#sales').classList.toggle('empty', !sales.length);
    $('#sales').innerHTML = sales.length ? sales.slice(0, 8).map(s => `<div class="sale"><span><strong>${escapeHtml(s.medicineName)}</strong> · ${s.quantity} sold</span><span>${formatMoney(s.total)} · ${new Date(s.soldAt).toLocaleString('en-IN')}</span></div>`).join('') : 'No sales recorded yet.';
}

$('#show-form').onclick = () => $('#medicine-form').classList.remove('hidden');
$('#close-form').onclick = () => $('#medicine-form').classList.add('hidden');
$('#search').oninput = e => { clearTimeout(searchTimer); searchTimer = setTimeout(() => loadMedicines(e.target.value), 250); };
$('#refresh-sales').onclick = loadSales;
$('#add-form').onsubmit = async e => {
    e.preventDefault(); const values = Object.fromEntries(new FormData(e.target));
    values.quantity = Number(values.quantity); values.price = Number(values.price);
    const response = await fetch('/api/medicines', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(values) });
    const message = $('#form-message');
    if (!response.ok) { const error = await response.json(); message.textContent = Object.values(error.errors || {}).flat().join(' '); return; }
    e.target.reset(); message.textContent = 'Medicine saved.'; $('#medicine-form').classList.add('hidden'); loadMedicines($('#search').value);
};
medicinesBody.onclick = async e => {
    const button = e.target.closest('.sell'); if (!button) return;
    const quantity = Number(prompt(`How many ${button.dataset.name} were sold? (Available: ${button.dataset.stock})`));
    if (!Number.isInteger(quantity) || quantity < 1) return;
    const response = await fetch(`/api/medicines/${button.dataset.id}/sales`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ quantity }) });
    if (!response.ok) { alert((await response.json()).message || 'Unable to record sale.'); return; }
    loadMedicines($('#search').value); loadSales();
};
loadMedicines(); loadSales();
