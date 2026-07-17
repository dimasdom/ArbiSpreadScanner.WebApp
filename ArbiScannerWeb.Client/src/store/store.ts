import { configureStore, createListenerMiddleware, isAnyOf } from '@reduxjs/toolkit';
import accountReducer, { logout, clearUserData } from './slices/accountSlice';
import { accountApi } from './services/account';
import { spreadApi } from './services/spread';
import { subscriptionsAPI } from './services/subscription';

const rootReducer = {
    account: accountReducer,
    [accountApi.reducerPath]: accountApi.reducer,
    [spreadApi.reducerPath]: spreadApi.reducer,
    [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer,
};

const sessionListenerMiddleware = createListenerMiddleware();
sessionListenerMiddleware.startListening({
    matcher: isAnyOf(logout, clearUserData),
    effect: (_action, listenerApi) => {
        listenerApi.dispatch(subscriptionsAPI.util.resetApiState());
    },
});

const store = configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware({
            serializableCheck: false,
        })
            .prepend(sessionListenerMiddleware.middleware)
            .concat(accountApi.middleware, spreadApi.middleware, subscriptionsAPI.middleware),
});

export type AppDispatch = typeof store.dispatch;
export type IRootStore = ReturnType<typeof store.getState>;

export default store;