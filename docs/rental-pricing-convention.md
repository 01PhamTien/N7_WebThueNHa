# Quy ước tính tiền thuê nhà

Hệ thống này phục vụ thuê nhà theo **giá thuê tháng**, không phải đặt phòng tính tiền theo đêm.

## Quy tắc thống nhất

- Ngày bắt đầu được tính tiền; ngày kết thúc không tính tiền (khoảng thuê dạng [ngày bắt đầu, ngày kết thúc)).
- Mỗi tháng tròn được tính theo giá thuê tháng, tính từ ngày bắt đầu ban đầu.
- Thời hạn ngắn hơn một tháng vẫn áp dụng **mức thuê tối thiểu bằng một tháng**.
- Sau các tháng tròn, ngày lẻ được tính theo công thức: giá thuê tháng / 30 × số ngày lẻ.
- Tiền thuê được làm tròn đến đồng gần nhất; không tự cộng tiền điện, nước, phí dịch vụ hoặc tiền cọc vào tiền thuê nếu chưa có khoản mục riêng.

## Ví dụ

Giá thuê niêm yết: 6.000.000 đ/tháng.

| Thời hạn | Cách tính | Tiền thuê |
|---|---:|---:|
| 2 ngày | Mức tối thiểu 1 tháng | 6.000.000 đ |
| 15 ngày | Mức tối thiểu 1 tháng | 6.000.000 đ |
| 1 tháng | 1 × 6.000.000 đ | 6.000.000 đ |
| 1 tháng + 10 ngày | 6.000.000 + 6.000.000 / 30 × 10 | 8.000.000 đ |

## Lưu ý nghiệp vụ

- Giao diện đặt thuê, API ước tính, hợp đồng và hóa đơn phải sử dụng cùng hàm `RentalPricing.Calculate`; không tự tính lại ở frontend.
- Giá thuê tháng được chốt trong yêu cầu/hợp đồng. Khi chủ nhà chấp nhận mức giá đề xuất, hệ thống tính lại theo giá đã chốt.
- Nếu sau này cần cho thuê ngắn ngày như căn hộ dịch vụ/khách sạn, cần bổ sung loại hình và bảng giá theo ngày riêng; không thay đổi ngầm quy tắc thuê nhà theo tháng.

## Checklist kiểm thử

1. 2 ngày: tính đúng 1 tháng.
2. 15 ngày: tính đúng 1 tháng.
3. Từ ngày 01 đến ngày 01 tháng sau: tính đúng 1 tháng (ngày kết thúc không bao gồm).
4. 1 tháng + 10 ngày: cộng thêm 10/30 giá tháng.
5. Mốc ngày 29, 30, 31 qua tháng 2: không bị trôi ngày kỳ tháng.
6. Ngày kết thúc bằng hoặc trước ngày bắt đầu, hoặc giá tháng không dương: trả về 0.
