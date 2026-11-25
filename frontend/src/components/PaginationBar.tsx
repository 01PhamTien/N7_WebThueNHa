import { Pagination } from 'antd';

export function PaginationBar({
  page,
  pageSize,
  total,
  onChange
}: {
  page: number;
  pageSize: number;
  total: number;
  onChange: (page: number) => void;
}) {
  if (total <= pageSize) return null;

  return (
    <div className="pagination">
      <Pagination
        current={page}
        pageSize={pageSize}
        total={total}
        showSizeChanger={false}
        showTotal={count => `Tổng ${count} mục`}
        onChange={onChange}
      />
    </div>
  );
}
