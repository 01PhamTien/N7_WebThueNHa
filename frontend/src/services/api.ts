import axios,{type AxiosError}from'axios';
export const TOKEN_KEY='web2_access_token';
export const api=axios.create({baseURL:import.meta.env.VITE_API_BASE_URL||''});
const messages:Record<number,string>={400:'Thông tin gửi lên chưa hợp lệ. Vui lòng kiểm tra lại.',401:'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',403:'Bạn không có quyền thực hiện thao tác này.',404:'Không tìm thấy dữ liệu yêu cầu.',409:'Dữ liệu đã thay đổi hoặc thao tác đang xung đột. Vui lòng tải lại.',500:'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'};
export function errorText(error:unknown):string{
 if(axios.isAxiosError(error)){const data=error.response?.data as {message?:string;title?:string}|undefined;return data?.message||data?.title||messages[error.response?.status||0]||'Không thể kết nối máy chủ. Vui lòng thử lại.'}
 return error instanceof Error?error.message:'Đã có lỗi xảy ra.';
}
api.interceptors.request.use(config=>{const token=sessionStorage.getItem(TOKEN_KEY);if(token)config.headers.Authorization='Bearer '+token;else delete config.headers.Authorization;return config});
api.interceptors.response.use(response=>response,error=>{const e=error as AxiosError;if(e.response?.status===401&&sessionStorage.getItem(TOKEN_KEY)){sessionStorage.removeItem(TOKEN_KEY);window.dispatchEvent(new Event('web2:unauthorized'))}return Promise.reject(error)});
