import{Pagination}from'antd';
export function PaginationBar({page,pageSize,total,onChange}:{page:number;pageSize:number;total:number;onChange:(page:number)=>void}){return total>pageSize?<div className="pagination"><Pagination current={page} pageSize={pageSize} total={total} showSizeChanger={false} showTotal={n=>`Tổng ${n} mục`} onChange={onChange}/></div>:null}
