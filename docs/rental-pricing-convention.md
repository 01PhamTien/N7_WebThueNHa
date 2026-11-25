# Quy ước giá thuê và các khoản phí

Hệ thống phục vụ thuê nhà theo tháng, không phải đặt phòng theo đêm.

## 1. Giá thuê
- Giá thuê/tháng do chủ nhà niêm yết trên tin đăng.
- Khách thuê không nhập hoặc đề xuất giá riêng khi gửi yêu cầu.
- Khi tạo yêu cầu, hệ thống chụp lại giá niêm yết tại thời điểm đặt; hợp đồng sử dụng giá đã chụp đó để tránh thay đổi giá giữa chừng.
- Ngày bắt đầu được tính tiền; ngày kết thúc không tính tiền (khoảng thuê dạng [ngày bắt đầu, ngày kết thúc)).
- Thời hạn ngắn hơn một tháng áp dụng mức thuê tối thiểu một tháng.
- Mỗi tháng tròn tính bằng giá thuê tháng; ngày lẻ sau tháng tròn tính theo giá thuê tháng / 30 × số ngày lẻ.
- Tiền thuê làm tròn đến đồng gần nhất.

## 2. Tiền cọc
- Chủ nhà khai báo tiền cọc cụ thể trên tin đăng; có thể bằng 0 nếu không yêu cầu cọc.
- Tiền cọc là khoản riêng, không cộng vào tiền thuê.
- Hệ thống phát hành hóa đơn cọc riêng sau khi hai bên ký hợp đồng.
- Giá trị cọc trong hợp đồng được lấy từ tin đăng tại lúc chủ nhà chấp nhận yêu cầu và có thể được hai bên rà soát trước khi ký.
- Dữ liệu tin đăng cũ được chuyển mặc định sang mức cọc bằng một tháng tiền thuê; chủ nhà có thể chỉnh sửa.

## 3. Số người ở
- Tin đăng khai báo sức chứa tối đa.
- Khách chọn số người ở thực tế khi gửi yêu cầu.
- Backend từ chối yêu cầu vượt sức chứa; không chỉ dựa vào kiểm tra ở giao diện.
- Không tự động cộng phụ phí người thứ ba/thứ tư. Nếu có phụ phí người ở thêm, chủ nhà phải công bố rõ và ghi thành điều khoản/khoản phí riêng trước khi ký.

## 4. Điện, nước và phí dịch vụ
- Không tự gộp điện, nước, internet, gửi xe hoặc phí quản lý vào tiền thuê tháng.
- Chủ nhà công bố đơn giá và cách tính trên tin đăng, ví dụ điện theo kWh, nước theo đồng hồ hoặc theo người, gửi xe theo xe/tháng.
- Mức phí áp dụng phải được ghi lại trong hợp đồng trước khi ký.
- Khoản điện/nước thực tế được lập hóa đơn riêng theo chỉ số hoặc số lượng sử dụng. Không tự tính từ phần mô tả văn bản của tin đăng.
- Gửi yêu cầu thuê không đồng nghĩa khách đã phải thanh toán; tiền thuê kỳ đầu và tiền cọc chỉ phát sinh hóa đơn sau khi hợp đồng được hai bên ký.

## 5. Ví dụ
Giá niêm yết 6.000.000 đ/tháng, cọc 6.000.000 đ, thuê 3 tháng:
- Tiền thuê 3 tháng: 18.000.000 đ.
- Tiền cọc: 6.000.000 đ (hóa đơn riêng).
- Thanh toán ban đầu nếu hợp đồng yêu cầu cọc một tháng và thu tháng đầu: 12.000.000 đ.
- Điện, nước và gửi xe: tính riêng theo mức đã ghi trong hợp đồng.

Với kỳ thuê 2 ngày, theo quy ước thuê nhà dài hạn hiện tại, tiền thuê tối thiểu vẫn bằng một tháng. Nếu muốn hỗ trợ thuê ngắn ngày như căn hộ dịch vụ, cần xây dựng loại hình và bảng giá theo ngày riêng, không thay đổi ngầm quy tắc này.

## 6. Nguyên tắc triển khai
- Giao diện ước tính, API, hợp đồng và hóa đơn phải dùng cùng quy ước `RentalPricing.Calculate`.
- Không nhận giá thuê từ trình duyệt khi tạo yêu cầu; backend lấy giá niêm yết từ Property.
- Không phát sinh phí không được công bố hoặc chưa được hai bên chấp thuận.
