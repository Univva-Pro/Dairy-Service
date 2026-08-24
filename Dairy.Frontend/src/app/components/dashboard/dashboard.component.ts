import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ProductService } from '../../services/product.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  products: any[] = [];
  username = '';
  role = '';
  isAdmin = false;
  showModal = false;

  newProduct: any = {
    name: '',
    fatContentPercentage: null,
    storageTemperatureRange: '',
    stockQuantity: null
  };

  constructor(
    private authService: AuthService,
    private productService: ProductService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.username = this.authService.getUsername() || '';
    this.role = this.authService.getRole() || '';
    this.isAdmin = this.authService.isAdmin();
    
    this.loadProducts();
  }

  loadProducts(): void {
    this.productService.getProducts().subscribe({
      next: (data) => {
        this.products = data || [];
      },
      error: (err) => {
        if (err?.status === 401 || err?.status === 403) {
          this.authService.logout();
          this.router.navigate(['/login']);
        } else {
          console.error('Error loading products:', err);
        }
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  isEditing = false;
  editingId: string | null = null;

  openModal(): void {
    this.isEditing = false;
    this.editingId = null;
    this.showModal = true;
  }

  openEditModal(p: any): void {
    this.isEditing = true;
    this.editingId = p.productId || p.id || p.ProductId;
    this.newProduct = {
      name: p.name || '',
      fatContentPercentage: p.fatContentPercentage ?? p.fatContent ?? 0,
      storageTemperatureRange: p.storageTemperatureRange || p.temperatureRequired || '2°C - 4°C',
      stockQuantity: p.stockQuantity ?? 0
    };
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.isSubmitting = false;
    this.isEditing = false;
    this.editingId = null;
    this.newProduct = {
      name: '',
      fatContentPercentage: null,
      storageTemperatureRange: '',
      stockQuantity: null
    };
  }

  isSubmitting = false;

  saveProduct(): void {
    if (this.isSubmitting) return;
    if (!this.newProduct.name?.trim()) return;

    this.isSubmitting = true;
    const payload = {
      name: this.newProduct.name.trim(),
      fatContentPercentage: Number(this.newProduct.fatContentPercentage) || 0,
      storageTemperatureRange: this.newProduct.storageTemperatureRange || '2°C - 4°C',
      stockQuantity: Number(this.newProduct.stockQuantity) || 0
    };

    if (this.isEditing && this.editingId) {
      this.productService.updateProduct(this.editingId, payload).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.closeModal();
          this.loadProducts();
        },
        error: (err) => {
          this.isSubmitting = false;
          console.error('Error updating product:', err);
        }
      });
    } else {
      this.productService.addProduct(payload).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.closeModal();
          this.loadProducts();
        },
        error: (err) => {
          this.isSubmitting = false;
          console.error('Error saving product:', err);
        }
      });
    }
  }

  deleteProduct(item: any): void {
    const id = typeof item === 'string' ? item : (item?.productId || item?.id || item?.ProductId);
    if (!id) return;

    this.productService.deleteProduct(id).subscribe({
      next: () => {
        this.loadProducts();
      },
      error: (err) => console.error('Error deleting product:', err)
    });
  }
}
