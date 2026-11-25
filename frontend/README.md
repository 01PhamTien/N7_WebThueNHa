# Frontend Web2_ThueNha

React + TypeScript + Vite + Ant Design 6.

## Chạy local

Yêu cầu Node.js 20.19+ hoặc 22.12+.

    cd frontend
    npm install
    cp .env.example .env.local
    npm run dev

Vite proxy tự chuyển tiếp `/api` và `/hubs` đến Backend. Mặc định proxy dùng profile HTTPS `https://localhost:7295` trong `Properties/launchSettings.json`.

- Nếu chạy profile HTTP trong Visual Studio, đặt `VITE_PROXY_TARGET=http://localhost:5196` trong `.env.local`.
- Để `VITE_API_BASE_URL` trống khi chạy local để request đi qua proxy và tránh lỗi CORS/404 từ Vite.
- Khi triển khai production, đặt `VITE_API_BASE_URL` thành URL gốc của Backend.

Build production:

    npm run build

## Cấu trúc mã nguồn
- `src/app`: khởi tạo Router, AuthContext, ProtectedRoute, RoleGuard và trang 403/404.
- `src/features/public`: trang chủ, tìm kiếm và chi tiết Property.
- `src/features/auth`: đăng nhập, đăng ký, hồ sơ.
- `src/features/tenant`: Favorites, Booking, Contract, Invoice, Notification và Chat.
- `src/layouts`: AppLayout và điều hướng responsive.
- `src/components`: component dùng chung, phân trang.
- `src/services`: Axios client, Auth service và Tenant API service.
- `src/types`: TypeScript interfaces khớp các DTO Backend.

## Xác thực và xử lý lỗi
AuthContext khôi phục phiên qua `GET /api/auth/me`, cung cấp login/register/logout/refresh. JWT lưu trong sessionStorage; Axios tự gắn Bearer token. Khi Backend trả 401, token và user trong AuthContext được xóa đồng bộ, giao diện không tiếp tục hiển thị tài khoản cũ. Lỗi 400/401/403/404/409/500 có thông báo tiếng Việt mặc định; thông điệp `message` từ Backend được ưu tiên.
