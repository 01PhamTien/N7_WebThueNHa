namespace Web2_ThueNha.Models;

public enum PropertyModerationStatus { Draft = 0, Pending = 1, Approved = 2, Rejected = 3 }
public enum PropertyListingStatus { Draft = 0, Published = 1, Hidden = 2, Closed = 3 }
public enum BookingStatus { Pending = 0, Accepted = 1, Rejected = 2, Cancelled = 3, Expired = 4 }
public enum RentalContractStatus { Draft = 0, AwaitingSignature = 1, Active = 2, Completed = 3, Terminated = 4, Cancelled = 5 }
public enum InvoiceType { Deposit = 0, MonthlyRent = 1, Other = 2 }
public enum InvoiceStatus { Draft = 0, Issued = 1, PartiallyPaid = 2, Paid = 3, Overdue = 4, Cancelled = 5 }
public enum PaymentMethod { Cash = 0, BankTransfer = 1, VNPay = 2, MoMo = 3, Other = 4 }
public enum PaymentStatus { Pending = 0, Succeeded = 1, Failed = 2, Cancelled = 3, Refunded = 4 }
public enum MessageType { Text = 0, Image = 1, File = 2, System = 3 }
public enum NotificationType { Booking = 0, Contract = 1, Invoice = 2, Payment = 3, Message = 4, System = 5 }
public enum ModerationAction { Approved = 0, Rejected = 1, Hidden = 2, Restored = 3 }
