# Frontend Web2_ThueNha

React + TypeScript + Vite + Ant Design.

## Chạy local
Yêu cầu Node.js 20.19+ hoặc 22.12+.

    cd frontend
    npm install
    cp .env.example .env.local
    npm run dev

Cấu hình VITE_API_BASE_URL trong .env.local bằng URL gốc Backend, ví dụ https://localhost:7000. Build production: npm run build.

## Cấu trúc và tích hợp
- src/app: Router, AuthContext, ProtectedRoute, RoleGuard.
- src/layouts: AppLayout.
- src/components: thành phần dùng chung được tổ chức trong app hiện tại.
- src/pages: giao diện Public và Tenant.
- src/services: Axios instance và API modules.
- src/theme: cấu hình Ant Design.

AuthContext cung cấp useAuth, login, register, logout, refresh. Axios tự gắn Bearer JWT từ sessionStorage và xử lý 401. Backend luôn cấp role Tenant khi đăng ký công khai. Các API tích hợp: Auth, Property public, Favorites, Booking Tenant, Contract/ký, Invoice/SePay, Notification và Chat REST/SignalR/tệp. App Layout, AuthContext, Axios và guard là nền tảng dùng chung để mở rộng Dashboard Landlord/Admin trên cùng project.


## Dashboard Landlord/Admin (Issue #42)
Các route được bảo vệ bằng `RoleGuard` và dùng chung AuthContext, Axios instance, theme, AppLayout:
- Landlord: `/landlord`, `/landlord/properties`, `/landlord/bookings`, `/landlord/contracts`, `/landlord/invoices`, `/landlord/chat`, `/landlord/notifications`.
- Admin: `/admin`, `/admin/users`, `/admin/moderation`.

Các module gọi API qua `src/services/landlordAdmin.ts`. Dashboard chỉ hiển thị số liệu mà API hiện có trả về. Thao tác xác nhận/từ chối thanh toán chỉ xuất hiện cho Payment có `PaymentProvider = Manual`; SePay được để Backend/IPN xử lý. Các form tạo/sửa tin đăng và thao tác ảnh tuân theo quy tắc Backend: nội dung thay đổi sẽ cần kiểm duyệt lại.
