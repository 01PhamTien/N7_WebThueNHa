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

## Dashboard Landlord/Admin (Issue #42)
Các route được bảo vệ bằng `RoleGuard` và dùng chung AuthContext, Axios instance, theme, AppLayout:
- Landlord: `/landlord`, `/landlord/properties`, `/landlord/bookings`, `/landlord/contracts`, `/landlord/invoices`, `/landlord/chat`, `/landlord/notifications`.
- Admin: `/admin`, `/admin/users`, `/admin/moderation`.

Các module gọi API qua `src/services/landlordAdmin.ts`. Dashboard chỉ hiển thị số liệu mà API hiện có trả về. Thao tác xác nhận/từ chối thanh toán chỉ xuất hiện cho Payment có `PaymentProvider = Manual`; SePay được để Backend/IPN xử lý. Các form tạo/sửa tin đăng và thao tác ảnh tuân theo quy tắc Backend: nội dung thay đổi sẽ cần kiểm duyệt lại.

## Dashboard Landlord/Admin (Issue #46)
- `src/features/landlord-admin/Workspace.tsx` là barrel export; trang Chủ nhà, trang Admin và tiện ích dùng chung được tách lần lượt vào `LandlordWorkspace.tsx`, `AdminWorkspace.tsx` và `workspaceShared.tsx`.
- `src/services/landlordAdmin.ts` khai báo DTO và gom toàn bộ request của hai vai trò; component không gọi Axios trực tiếp.
- Các bảng dùng metadata `totalCount` hoặc `totalItems` từ Backend. Giao diện hỗ trợ các bộ lọc trạng thái/loại mà API hiện cung cấp; Admin user và moderation có tìm kiếm theo các tham số API hỗ trợ.
- Chat Chủ nhà đồng bộ sự kiện gửi/sửa/xóa qua SignalR, cho phép sửa/xóa tin nhắn của chính người gửi, đánh dấu hội thoại đã đọc và kiểm tra tệp đính kèm trước khi upload.
- Form hợp đồng và hóa đơn kiểm tra dữ liệu theo DTO Backend; hóa đơn hiển thị dòng chi tiết và các khoản thanh toán. Chỉ Payment `Manual` ở trạng thái `Pending` mới có thao tác xác nhận/từ chối thủ công; SePay cập nhật qua IPN/Bank Webhook.
- Hạn chế API hiện tại: danh sách Property của Chủ nhà, Booking, Contract và Invoice chưa có tham số tìm kiếm keyword; API moderation chưa có bộ lọc `PropertyListingStatus` để phân trang riêng tin Hidden. Không giả lập hai chức năng này ở client.
- Hướng dẫn thao tác dành cho người dùng: `../docs/huong-dan-nguoi-dung/landlord-admin.md`.
