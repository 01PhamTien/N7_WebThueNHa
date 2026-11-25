# Hướng dẫn sử dụng dành cho Chủ nhà và Quản trị viên

Tài liệu này mô tả các thao tác hiện có trên giao diện Web2_ThueNHa. Một số trạng thái được giao diện hiển thị bằng tên tiếng Anh như `Pending`, `Accepted`, `Draft`; phần giải thích bên dưới dùng tiếng Việt để người dùng dễ theo dõi.

## Mục lục

- [1. Đăng nhập và truy cập](#1-đăng-nhập-và-truy-cập)
- [2. Hướng dẫn dành cho Chủ nhà](#2-hướng-dẫn-dành-cho-chủ-nhà)
  - [2.1. Tổng quan Dashboard](#21-tổng-quan-dashboard)
  - [2.2. Quản lý tin đăng và hình ảnh](#22-quản-lý-tin-đăng-và-hình-ảnh)
  - [2.3. Xử lý yêu cầu thuê](#23-xử-lý-yêu-cầu-thuê)
  - [2.4. Quản lý hợp đồng](#24-quản-lý-hợp-đồng)
  - [2.5. Hóa đơn và thanh toán](#25-hóa-đơn-và-thanh-toán)
  - [2.6. Tin nhắn và tệp đính kèm](#26-tin-nhắn-và-tệp-đính-kèm)
  - [2.7. Thông báo](#27-thông-báo)
- [3. Hướng dẫn dành cho Admin](#3-hướng-dẫn-dành-cho-admin)
  - [3.1. Tổng quan Dashboard Admin](#31-tổng-quan-dashboard-admin)
  - [3.2. Quản lý tài khoản](#32-quản-lý-tài-khoản)
  - [3.3. Kiểm duyệt tin đăng](#33-kiểm-duyệt-tin-đăng)
- [4. Lưu ý khi thao tác](#4-lưu-ý-khi-thao-tác)

---

## 1. Đăng nhập và truy cập

1. Mở website Web2_ThueNHa.
2. Chọn **Đăng nhập** hoặc truy cập đường dẫn `/dang-nhap`.
3. Nhập email và mật khẩu của tài khoản đã được cấp vai trò **Landlord** (Chủ nhà) hoặc **Admin** (Quản trị viên).
4. Chọn **Đăng nhập**.

Sau khi đăng nhập:
- Tài khoản có vai trò **Landlord** được chuyển đến Dashboard chủ nhà tại `/landlord`.
- Tài khoản có vai trò **Admin** được chuyển đến Dashboard Admin tại `/admin`.
- Menu Chủ nhà và Admin chỉ xuất hiện khi tài khoản có vai trò tương ứng. Nếu tài khoản có cả hai vai trò, người dùng có thể truy cập cả hai nhóm chức năng.
- Nếu truy cập chức năng không thuộc quyền của tài khoản, hệ thống sẽ chặn truy cập.

Nếu chưa có tài khoản, có thể chọn **Đăng ký** tại `/dang-ky`. Để sử dụng khu vực Chủ nhà hoặc Admin, tài khoản cần được cấp đúng vai trò.

## 2. Hướng dẫn dành cho Chủ nhà

### 2.1. Tổng quan Dashboard

1. Đăng nhập bằng tài khoản có vai trò Landlord.
2. Chọn **Dashboard chủ nhà** trên menu hoặc truy cập `/landlord`.
3. Dashboard hiển thị các số liệu lấy từ API:
   - **Tin đăng**: tổng số tin thuộc tài khoản.
   - **Booking chờ xử lý**: số yêu cầu thuê đang Pending.
   - **Hợp đồng hiệu lực**: số hợp đồng Active.
   - **Hóa đơn quá hạn**: số hóa đơn Overdue.
4. Dùng các liên kết nhanh để mở **Tin đăng**, **Yêu cầu thuê**, **Hợp đồng** hoặc **Hóa đơn**.

### 2.2. Quản lý tin đăng và hình ảnh

Mở **Tin đăng** trên menu hoặc truy cập `/landlord/properties`.

**Tìm kiếm và lọc**

- Nhập từ khóa để tìm theo tiêu đề hoặc địa chỉ căn nhà; kết quả được lọc tại Backend và kết hợp với phân trang.
- Dùng bộ lọc trạng thái hiển thị: Draft, Published, Hidden hoặc Closed.

**Tạo tin đăng**

1. Chọn **Tạo tin đăng**.
2. Nhập các thông tin được yêu cầu:
   - Mã loại nhà.
   - Tiêu đề.
   - Giá thuê mỗi tháng.
   - Diện tích.
   - Số phòng ngủ và phòng tắm.
   - Địa chỉ và tỉnh/thành phố.
   - Có thể bổ sung mô tả và phường/xã.
3. Chọn **Lưu**.

Tin mới được tạo ở trạng thái chờ Admin kiểm duyệt (`Pending`) và chưa được đăng công khai (`Draft`).

**Chỉnh sửa tin đăng**

1. Tìm tin trong danh sách.
2. Chọn **Sửa**, cập nhật thông tin rồi chọn **Lưu**.

Sau khi cập nhật nội dung, tin chuyển về trạng thái chờ duyệt (`Pending`) và bản tin trở về `Draft`. Tin cần được Admin duyệt lại trước khi hiển thị công khai.

**Ẩn hoặc đăng công khai**

- Dùng danh sách trạng thái ở cột thao tác để chọn **Ẩn tin** hoặc **Đăng công khai**.
- Chỉ tin đã được Admin duyệt mới có thể chuyển sang trạng thái đăng công khai. Nếu tin chưa được duyệt, thao tác đăng công khai sẽ không thành công.
- Trạng thái hiển thị trong danh sách gồm `Draft` (bản nháp), `Published` (đang đăng), `Hidden` (đã ẩn) và `Closed` (đã đóng).

**Quản lý hình ảnh**

1. Chọn **Ảnh** tại tin đăng cần quản lý.
2. Để thêm ảnh, nhập URL ảnh HTTPS hợp lệ rồi chọn **Thêm ảnh**.
3. Chọn **Ảnh đại diện** để đặt ảnh chính.
4. Chọn **Xóa** và xác nhận nếu muốn xóa ảnh.

Mỗi tin đăng được tối đa 20 hình ảnh. Thêm, xóa hoặc đổi ảnh đại diện đều làm tin chuyển về trạng thái chờ duyệt (`Pending`) và `Draft`; tin cần được Admin duyệt lại.

### 2.3. Xử lý yêu cầu thuê

Mở **Yêu cầu thuê** tại `/landlord/bookings`.

- Nhập từ khóa để tìm theo tên/email người thuê hoặc tiêu đề căn nhà.
- Kết hợp từ khóa với bộ lọc trạng thái và phân trang.

1. Chọn bộ lọc trạng thái để xem yêu cầu đang chờ (`Pending`), đã chấp nhận (`Accepted`), đã từ chối (`Rejected`), đã hủy (`Cancelled`) hoặc hết hạn (`Expired`).
2. Xem thông tin trong danh sách: căn nhà, người thuê, thời gian thuê, giá đề xuất mỗi tháng, tổng tiền thuê ước tính cho kỳ và trạng thái.
3. Chọn **Chi tiết** để xem số người thuê và lời nhắn của người thuê.
4. Với yêu cầu đang **Pending**:
   - Chọn **Chấp nhận** và xác nhận để chấp nhận yêu cầu.
   - Chọn **Từ chối**, nhập lý do nếu muốn, sau đó xác nhận.
5. Với yêu cầu đã **Accepted**, chọn **Tạo hợp đồng** để tạo hợp đồng nháp từ yêu cầu đó.

Chỉ yêu cầu đang Pending mới có thể được chấp nhận hoặc từ chối. Hệ thống cũng kiểm tra xung đột thời gian thuê; nếu khoảng thời gian đã có booking được chấp nhận, thao tác có thể bị từ chối.

### 2.4. Quản lý hợp đồng

Mở **Hợp đồng** tại `/landlord/contracts`.

- Nhập từ khóa để tìm theo mã hợp đồng, tên người thuê hoặc tiêu đề căn nhà; có thể kết hợp bộ lọc trạng thái.

1. Danh sách hiển thị mã hợp đồng, căn nhà, người thuê, thời hạn, đơn giá mỗi tháng, tổng tiền thuê ước tính cả kỳ và trạng thái.
2. Hợp đồng mới tạo từ Booking Accepted ở trạng thái **Draft** (bản nháp).
3. Với hợp đồng Draft, chọn **Chỉnh sửa** để cập nhật:
   - Ngày bắt đầu và ngày kết thúc (ngày kết thúc không tính vào số ngày thuê).
   - Tiền thuê mỗi tháng.
   - Tiền đặt cọc.
   - Ngày đến hạn thanh toán hàng tháng (từ ngày 1 đến ngày 28).
   - Điều khoản.
4. Chọn **Lưu** để cập nhật bản nháp.
5. Chọn **Gửi ký** và xác nhận để chuyển hợp đồng sang trạng thái chờ chữ ký (`AwaitingSignature`).

Hợp đồng chỉ chuyển sang **Active** khi cả Chủ nhà và Người thuê đều ký xác nhận. Chủ nhà chỉ chỉnh sửa hoặc gửi ký khi hợp đồng còn ở trạng thái Draft.

### 2.5. Hóa đơn và thanh toán

Mở **Hóa đơn** tại `/landlord/invoices`.

- Nhập từ khóa để tìm theo mã hóa đơn, mã hợp đồng hoặc tiêu đề căn nhà; có thể kết hợp lọc trạng thái và loại hóa đơn.

**Xem hóa đơn**

- Danh sách hiển thị mã hóa đơn, căn nhà, loại hóa đơn, hạn thanh toán, tổng tiền và trạng thái.
- Chọn **Chi tiết & thanh toán** để xem thông tin hóa đơn và danh sách các khoản thanh toán.

**Tạo hóa đơn**

Giao diện hiện có nút **Tạo hóa đơn**. Biểu mẫu yêu cầu Contract ID (UUID), loại hóa đơn, ngày phát hành, hạn thanh toán và kỳ thanh toán (bắt buộc với tiền thuê hàng tháng). Hạn thanh toán không được trước ngày phát hành; ngày kết thúc kỳ phải sau ngày bắt đầu kỳ. Hợp đồng phải thuộc Chủ nhà và ở trạng thái **Active** (đã được cả hai bên ký) thì Backend mới cho phát hành hóa đơn.

> **Quy ước tiền thuê:** Mỗi tháng lịch tròn tính theo đơn giá tháng trong hợp đồng; phần ngày lẻ tính bằng đơn giá tháng chia 30 nhân số ngày lẻ. Ngày kết thúc kỳ không được tính vào số ngày thuê. Ví dụ, đơn giá 3.000.000đ/tháng thì 2 ngày là 200.000đ, 15 ngày là 1.500.000đ, một tháng tròn là 3.000.000đ và một tháng cộng 2 ngày là 3.200.000đ. Với hóa đơn **Tiền thuê hàng tháng**, Backend tự tính tiền từ hợp đồng, không dùng đơn giá do trình duyệt gửi lên; chỉ nhập một dòng tiền thuê. Phí điện, nước và khoản phát sinh lập hóa đơn loại **Khác**. Không thể tạo kỳ tiền thuê chồng lấn với hóa đơn chưa hủy.

Với loại **Khác**, nhập nội dung, số lượng và đơn giá của từng khoản. Hai trường kỳ thanh toán có thể để trống nếu khoản phí không gắn với một khoảng thời gian. Kiểm tra thông báo kết quả sau khi gửi để biết hóa đơn đã được tạo thành công hay chưa.

**Phân biệt Manual và SePay**

- **Manual (thủ công):** Chủ nhà có thể thấy nút **Xác nhận** và **Từ chối** khi khoản thanh toán đồng thời có nhà cung cấp `Manual` và trạng thái `Pending` (đang chờ). Chỉ sử dụng các nút này để xử lý khoản thanh toán thủ công đang chờ.
- **SePay:** Không xác nhận hoặc từ chối thủ công. Trạng thái thanh toán SePay được cập nhật thông qua IPN hoặc Bank Webhook sau khi hệ thống nhận và xác thực thông báo giao dịch.
- Khi thao tác xác nhận/từ chối Manual, hãy kiểm tra đúng khoản thanh toán và thông tin giao dịch trước khi thực hiện.

### 2.6. Tin nhắn và tệp đính kèm

Mở **Tin nhắn chủ nhà** tại `/landlord/chat`.

1. Chọn một hội thoại trong danh sách bên trái.
2. Nội dung hội thoại được tải ở khung tin nhắn. Số chưa đọc được thể hiện bằng nhãn đếm bên cạnh hội thoại.
3. Nhập nội dung và chọn **Gửi** (hoặc nhấn Enter) để gửi tin nhắn.
4. Chọn **Tệp** để gửi tệp đính kèm.
5. Chọn liên kết tên tệp trong hội thoại để tải/mở tệp.

Giới hạn tệp đính kèm theo Backend:
- Dung lượng tối đa **10 MB** cho mỗi tệp.
- Định dạng được hỗ trợ: **JPG/JPEG, PNG, WEBP, PDF, DOC, DOCX**.
- Phần mở rộng, kiểu nội dung và chữ ký tệp phải phù hợp; đổi tên đuôi tệp không hợp lệ sẽ không giúp tải lên thành công.

Khi mở hội thoại, hệ thống tải lịch sử tin nhắn và đánh dấu hội thoại đã đọc. Dùng bộ lọc **Hộp thư đến** để xem hội thoại đang hoạt động và **Đã lưu trữ** để xem các hội thoại đã cất đi. Trong hội thoại, chọn **Lưu trữ** để chuyển hội thoại khỏi hộp thư đến; chọn **Đưa về hộp thư đến** để tiếp tục trao đổi. Lưu trữ không xóa tin nhắn hoặc dữ liệu hội thoại. Bạn vẫn có thể gửi tin nhắn hoặc tệp trực tiếp từ hội thoại đã lưu trữ; khi gửi mới, hội thoại tự chuyển về Hộp thư đến của bạn, không thay đổi trạng thái lưu trữ của người thuê. Giao diện không cung cấp thao tác sửa hoặc xóa tin nhắn đã gửi. Tin nhắn mới được đồng bộ realtime khi kết nối hoạt động. Nếu tải/gửi tin nhắn hoặc tệp thất bại, giao diện hiển thị thông báo lỗi; hãy kiểm tra kết nối, quyền truy cập và định dạng/dung lượng tệp rồi thử lại.

### 2.7. Thông báo

Mở **Thông báo chủ nhà** tại `/landlord/notifications`.

- Danh sách hiển thị tiêu đề, nội dung, thời gian và trạng thái đã đọc/chưa đọc.
- Chọn **Đánh dấu đã đọc** trên một thông báo để cập nhật riêng thông báo đó.
- Chọn **Đánh dấu tất cả đã đọc** để cập nhật toàn bộ thông báo đang hiển thị theo thao tác của hệ thống.

## 3. Hướng dẫn dành cho Admin

### 3.1. Tổng quan Dashboard Admin

Đăng nhập bằng tài khoản có vai trò Admin, sau đó chọn **Dashboard Admin** hoặc truy cập `/admin`.

Dashboard hiển thị:
- **Tổng tài khoản**: số tài khoản lấy từ API quản lý người dùng.
- **Tin chờ duyệt**: số tin đăng đang chờ kiểm duyệt.

Dùng liên kết nhanh để mở **Quản lý người dùng** hoặc **Kiểm duyệt tin đăng**.

### 3.2. Quản lý tài khoản

Mở **Người dùng** tại `/admin/users`.

**Tìm kiếm và lọc**

1. Nhập tên, email hoặc số điện thoại vào ô tìm kiếm.
2. Nhấn Enter hoặc chọn biểu tượng tìm kiếm để thực hiện tìm kiếm.
3. Dùng bộ lọc trạng thái:
   - **Hoạt động**: tài khoản đang hoạt động.
   - **Đã khóa**: tài khoản bị khóa.
4. Dùng nút **Trước/Sau** để chuyển trang.

**Xem chi tiết và cập nhật vai trò**

1. Chọn **Chi tiết / Vai trò** tại tài khoản cần xem.
2. Kiểm tra họ tên, email, trạng thái, ngày tạo và danh sách vai trò.
3. Chọn vai trò phù hợp trong danh sách: Tenant, Landlord hoặc Admin.
4. Chọn **Lưu vai trò** để gửi cập nhật.

Người dùng cần đăng nhập lại để token nhận vai trò mới. Admin không thể tự thay đổi vai trò của chính mình.

**Khóa hoặc mở khóa tài khoản**

1. Chọn **Khóa** ở tài khoản đang hoạt động hoặc **Mở khóa** ở tài khoản đã khóa.
2. Đọc nội dung xác nhận và chọn đồng ý hoặc hủy.
3. Sau khi cập nhật thành công, danh sách được tải lại.

Các giới hạn bảo vệ tài khoản:
- Admin không thể tự khóa tài khoản đang đăng nhập.
- Không thể khóa Admin cuối cùng đang hoạt động.
- Không thể gỡ vai trò Admin của Admin cuối cùng đang hoạt động.
- Một số thao tác không hợp lệ có thể bị từ chối với thông báo từ Backend.

### 3.3. Kiểm duyệt tin đăng

Mở **Kiểm duyệt tin** tại `/admin/moderation`.

**Xem danh sách và chi tiết**

1. Dùng bộ lọc trạng thái kiểm duyệt để xem tin **Draft** (bản nháp), **Pending** (chờ duyệt), **Approved** (đã duyệt) hoặc **Rejected** (bị từ chối).
2. Có thể lọc thêm theo trạng thái hiển thị **Draft**, **Published**, **Hidden** hoặc **Closed**. Bộ lọc này có thể kết hợp với trạng thái kiểm duyệt và tìm kiếm.
3. Nhập từ khóa để tìm theo tiêu đề và/hoặc tỉnh/thành phố.
4. Danh sách hiển thị tiêu đề tin, chủ nhà, khu vực, giá thuê và trạng thái kiểm duyệt.
5. Chọn **Chi tiết** để xem thông tin căn nhà, địa chỉ, mô tả, giá, hình ảnh và lịch sử kiểm duyệt.
6. Lịch sử có thể hiển thị hành động, tên Admin, lý do và thời điểm thực hiện.

**Thực hiện hành động theo trạng thái**

- **Tin Pending:** có thể chọn **Duyệt** hoặc **Từ chối**.
  - Duyệt: xác nhận thao tác.
  - Từ chối: nhập lý do tối thiểu 5 ký tự rồi xác nhận.
- **Tin Approved và đang Published:** có thể chọn **Ẩn**. Nhập lý do tối thiểu 5 ký tự rồi xác nhận.
- **Tin Approved và đang Hidden:** có thể chọn **Khôi phục**. Nhập lý do tối thiểu 5 ký tự rồi xác nhận.

Nút thao tác chỉ xuất hiện khi trạng thái hiện tại cho phép hành động đó. Ví dụ, tin đã duyệt không còn nút Duyệt/Từ chối; tin đã ẩn không có nút Ẩn lần nữa.

**Phân biệt hai loại trạng thái**

- **Trạng thái kiểm duyệt:** Draft, Pending, Approved, Rejected.
- **Trạng thái hiển thị tin:** Draft, Published, Hidden, Closed.

Một tin có thể đã được duyệt (`Approved`) nhưng đang bị ẩn (`Hidden`). Khi chọn trạng thái hiển thị Published, Hidden hoặc Closed, giao diện tự chuyển bộ lọc kiểm duyệt sang Approved để hiển thị đúng nhóm tin tương ứng. Với Draft, bộ lọc kiểm duyệt chuyển về Pending; có thể thay đổi bộ lọc kiểm duyệt nếu cần xem nhóm khác.

## 4. Lưu ý khi thao tác

- Chỉ dùng chức năng thuộc vai trò được cấp. Người dùng không có quyền sẽ không truy cập được trang hoặc thao tác tương ứng.
- Sau khi tạo/cập nhật nội dung tin đăng hoặc thay đổi hình ảnh, tin có thể quay lại hàng chờ kiểm duyệt; cần chờ Admin duyệt trước khi đăng công khai.
- Không thể tạo hợp đồng từ Booking chưa được chấp nhận.
- Hợp đồng cần chữ ký của cả hai bên để chuyển sang Active.
- Chỉ khoản thanh toán **Manual + Pending** mới được xác nhận/từ chối thủ công. Không xác nhận thủ công giao dịch SePay.
- Với thao tác khóa tài khoản, cập nhật vai trò, duyệt/từ chối/ẩn/khôi phục tin, hãy kiểm tra đúng đối tượng và trạng thái trước khi xác nhận.
- Nếu giao diện hiển thị lỗi, đọc nội dung thông báo. Các lỗi thường gặp gồm dữ liệu không hợp lệ, không đủ quyền, không tìm thấy dữ liệu hoặc xung đột trạng thái; hãy tải lại trang và kiểm tra trạng thái hiện tại trước khi thử lại.
