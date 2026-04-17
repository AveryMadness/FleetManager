'use strict';

document.getElementById('sidebarToggle')?.addEventListener('click', () => {
  document.getElementById('sidebar')?.classList.toggle('open');
});

document.querySelectorAll('.alert-dismissible').forEach(el => {
  setTimeout(() => {
    const bsAlert = bootstrap.Alert.getOrCreateInstance(el);
    bsAlert?.close();
  }, 5000);
});

document.querySelectorAll('[data-confirm]').forEach(btn => {
  btn.addEventListener('click', e => {
    if (!confirm(btn.dataset.confirm)) e.preventDefault();
  });
});

const vehicleSelect = document.getElementById('vehicleSelect');
const startDateInput = document.getElementById('startDate');
const endDateInput   = document.getElementById('endDate');
const availBadge     = document.getElementById('availBadge');

async function checkAvailability() {
  const vid   = vehicleSelect?.value;
  const start = startDateInput?.value;
  const end   = endDateInput?.value;

  if (!vid || !start || !end || !availBadge) return;

  const res  = await fetch(`/Reservation/CheckAvailability?vehicleId=${vid}&start=${start}&end=${end}`);
  const data = await res.json();

  availBadge.textContent  = data.available ? '✓ Available' : '✗ Not available';
  availBadge.className    = 'badge ' + (data.available ? 'bg-success' : 'bg-danger');
}

vehicleSelect?.addEventListener('change', checkAvailability);
startDateInput?.addEventListener('change', checkAvailability);
endDateInput?.addEventListener('change',   checkAvailability);

const weeklyCanvas = document.getElementById('weeklyChart');
if (weeklyCanvas) {
  const labels = JSON.parse(weeklyCanvas.dataset.labels || '[]');
  const values = JSON.parse(weeklyCanvas.dataset.values || '[]');

  new Chart(weeklyCanvas, {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        label: 'Reservations',
        data: values,
        backgroundColor: 'rgba(83,74,183,0.18)',
        borderColor:     'rgba(83,74,183,0.85)',
        borderWidth: 2,
        borderRadius: 6,
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { beginAtZero: true, ticks: { stepSize: 1 }, grid: { color: '#f0eef8' } },
        x: { grid: { display: false } }
      }
    }
  });
}