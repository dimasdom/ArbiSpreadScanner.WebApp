import { configureStore } from '@reduxjs/toolkit';
import accountReducer from './slices/accountSlice';
import { accountApi } from './services/account';
import { spreadApi } from './services/spread';
import { subscriptionsAPI } from './services/subscription';

const rootReducer = {
    account: accountReducer,
    [accountApi.reducerPath]: accountApi.reducer,
    [spreadApi.reducerPath]: spreadApi.reducer,
    [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer,
};

const store = configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware({
            serializableCheck: false,
        }).concat(accountApi.middleware, spreadApi.middleware, subscriptionsAPI.middleware),
});

export type AppDispatch = typeof store.dispatch;
export type IRootStore = ReturnType<typeof store.getState>;

export default store;