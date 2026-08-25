const API_URL = '/api';

// Login Logic
const loginForm = document.getElementById('loginForm');
if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const username = document.getElementById('username').value;
        const password = document.getElementById('password').value;

        try {
            const res = await fetch(`${API_URL}/auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, password })
            });

            if (res.ok) {
                const data = await res.json();
                localStorage.setItem('token', data.token);
                localStorage.setItem('role', data.role);
                localStorage.setItem('username', data.username);
                window.location.href = '/dashboard.html';
            } else {
                document.getElementById('errorMsg').innerText = 'Invalid credentials';
            }
        } catch (err) {
            document.getElementById('errorMsg').innerText = 'Server error';
        }
    });
}

// Dashboard Logic
async function loadDashboard() {
    const token = localStorage.getItem('token');
    const role = localStorage.getItem('role');
    const username = localStorage.getItem('username');

    if (!token) {
        window.location.href = '/index.html';
        return;
    }

    document.getElementById('userRoleBadge').innerText = role;

    // Display Username
    const nameDisplay = document.getElementById('userNameDisplay');
    if (nameDisplay && username) {
        nameDisplay.innerText = `Welcome, ${username}`;
    }

    // Logout
    document.getElementById('logoutBtn').addEventListener('click', () => {
        localStorage.clear();
        window.location.href = '/index.html';
    });

    const isAdmin = true;
    if (isAdmin) {
        document.getElementById('addBtn')?.classList.remove('hidden');
    }

    // Setup Table Headers based on role
    const tableHeader = document.getElementById('tableHeader');
    if (tableHeader) {
        if (isAdmin) {
            tableHeader.innerHTML = `
                <th>Product Name</th>
                <th>Fat %</th>
                <th>Temp Required</th>
                <th>Stock</th>
                <th>Fresh?</th>
                <th>Actions</th>
            `;
        } else {
            tableHeader.innerHTML = `
                <th>Product Name</th>
                <th>Fat %</th>
                <th>Fresh?</th>
            `;
        }
    }

    await fetchProducts(token, isAdmin);

    // Modal UI binding
    if (isAdmin) {
        document.getElementById('addBtn')?.addEventListener('click', () => {
            document.getElementById('addProductModal')?.classList.remove('hidden');
        });
        document.getElementById('cancelBtn')?.addEventListener('click', () => {
            document.getElementById('addProductModal')?.classList.add('hidden');
        });

        document.getElementById('saveProductBtn')?.addEventListener('click', async () => {
            const payload = {
                name: document.getElementById('pName').value,
                fatContentPercentage: parseFloat(document.getElementById('pFat').value),
                storageTemperatureRange: document.getElementById('pTemp').value,
                stockQuantity: parseInt(document.getElementById('pStock').value)
            };

            await fetch(`${API_URL}/dairy/products`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(payload)
            });

            document.getElementById('addProductModal')?.classList.add('hidden');
            await fetchProducts(token, isAdmin);
        });

        document.getElementById('cancelEditBtn')?.addEventListener('click', () => {
            document.getElementById('editProductModal')?.classList.add('hidden');
        });

        document.getElementById('updateProductBtn')?.addEventListener('click', async () => {
            const id = document.getElementById('editId').value;
            const payload = {
                name: document.getElementById('editName').value,
                fatContentPercentage: parseFloat(document.getElementById('editFat').value),
                storageTemperatureRange: document.getElementById('editTemp').value,
                stockQuantity: parseInt(document.getElementById('editStock').value)
            };

            await fetch(`${API_URL}/dairy/products/${id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(payload)
            });

            document.getElementById('editProductModal')?.classList.add('hidden');
            await fetchProducts(token, isAdmin);
        });
    }
}

async function fetchProducts(token, isAdmin) {
    try {
        const res = await fetch(`${API_URL}/dairy/products`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (res.status === 401) {
            window.location.href = '/index.html';
            return;
        }

        const products = await res.json();
        const tbody = document.getElementById('productsBody');
        if (!tbody) return;
        tbody.innerHTML = '';

        products.forEach(p => {
            const tr = document.createElement('tr');
            const productId = p.productId || p.ProductId || p.id || p.Id;
            const name = p.name || p.Name || 'Dairy Product';
            const fatContent = p.fatContent !== undefined ? p.fatContent : (p.FatContent !== undefined ? p.FatContent : 0);
            const temp = p.temperatureRequired || p.TemperatureRequired || p.storageTemperatureRange || '2°C - 4°C';
            const stock = p.stockQuantity !== undefined ? p.stockQuantity : (p.StockQuantity !== undefined ? p.StockQuantity : 0);
            const isFresh = p.isFresh !== undefined ? p.isFresh : (p.IsFresh !== undefined ? p.IsFresh : true);

            const escapedName = name.replace(/'/g, "\\'");
            const escapedTemp = temp.replace(/'/g, "\\'");

            if (isAdmin) {
                const btnEdit = document.createElement('button');
                btnEdit.className = 'btn-primary btn-sm';
                btnEdit.style.marginRight = '5px';
                btnEdit.innerText = 'Edit';
                btnEdit.onclick = () => window.openEditModal(productId, name, fatContent, temp, stock);

                const btnDelete = document.createElement('button');
                btnDelete.className = 'btn-secondary btn-sm';
                btnDelete.innerText = 'Delete';
                btnDelete.onclick = () => window.deleteProduct(productId);

                const tdActions = document.createElement('td');
                tdActions.appendChild(btnEdit);
                tdActions.appendChild(btnDelete);

                tr.innerHTML = `
                    <td>${name}</td>
                    <td>${fatContent}%</td>
                    <td>${temp}</td>
                    <td>${stock}</td>
                    <td>${isFresh ? 'Yes' : 'No'}</td>
                `;
                tr.appendChild(tdActions);
            } else {
                tr.innerHTML = `
                    <td>${name}</td>
                    <td>${fatContent}%</td>
                    <td>${isFresh ? 'Yes' : 'No'}</td>
                `;
            }
            tbody.appendChild(tr);
        });
    } catch (err) {
        console.error(err);
    }
}

window.openEditModal = (id, name, fat, temp, stock) => {
    const editId = document.getElementById('editId');
    const editName = document.getElementById('editName');
    const editFat = document.getElementById('editFat');
    const editTemp = document.getElementById('editTemp');
    const editStock = document.getElementById('editStock');

    if (editId) editId.value = id;
    if (editName) editName.value = name;
    if (editFat) editFat.value = fat;
    if (editTemp) editTemp.value = temp;
    if (editStock) editStock.value = stock;

    document.getElementById('editProductModal')?.classList.remove('hidden');
};

window.deleteProduct = async (id) => {
    const token = localStorage.getItem('token');
    await fetch(`${API_URL}/dairy/products/${id}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${token}` }
    });
    await fetchProducts(token, true);
};

if (window.location.pathname.includes('dashboard.html')) {
    loadDashboard();
}
