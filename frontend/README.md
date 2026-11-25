# Frontend Web2_ThueNha

React + TypeScript + Vite + Ant Design.

## Chạy local
Yêu cầu Node.js 20.19+ hoặc 22.12+.

    cd frontend
    npm install
    cp .env.example .env.local
    npm run dev

Cấu hình `VITE_API_BASE_URL` trong `.env.local` bằng URL gốc Backend, ví dụ `https://localhost:7000`. Build production: `npm run build`.

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

## Phân trang và Chat
Danh sách Property, Favorite, Booking, Contract, Invoice, Notification và Conversation/Message sử dụng metadata phân trang Backend. Bộ lọc Property đồng bộ query string, giữ nguyên khi chuyển trang và mở/đóng chi tiết. Chat hỗ trợ lịch sử theo trang, realtime SignalR, đánh dấu đã đọc, lưu trữ/khôi phục hội thoại, sửa/xóa tin nhắn của chính người gửi và upload tệp. Tin nhắn văn bản chỉ xuất hiện sau khi REST API xác nhận thành công; sự kiện realtime được khử trùng lặp theo `messageId`.

## Quy ước
Giữ API calls trong service module; không ghép endpoint trong component. Không thay đổi API contract, framework hoặc UI library của dự án. Chạy `npm run build` trước khi bàn giao.
