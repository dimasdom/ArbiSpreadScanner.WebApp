import { useDispatch, useSelector } from 'react-redux';
import type { AppDispatch, IRootStore } from './store/store';

export const useAppDispatch = () => useDispatch<AppDispatch>();
export const useAppSelector = useSelector.withTypes<IRootStore>();
