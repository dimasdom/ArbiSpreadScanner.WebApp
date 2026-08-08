import { configureStore, createListenerMiddleware } from '@reduxjs/toolkit';
import accountReducer, { logout } from './slices/accountSlice';
import languageReducer from './slices/languageSlice';
import { accountApi } from './services/account';
import { spreadApi } from './services/spread';
import { subscriptionsAPI } from './services/subscription';
import { mcpTokenApi } from './services/mcpToken';

const rootReducer = {
    account: accountReducer,
    language: languageReducer,
    [accountApi.reducerPath]: accountApi.reducer,
    [spreadApi.reducerPath]: spreadApi.reducer,
    [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer,
    [mcpTokenApi.reducerPath]: mcpTokenApi.reducer,
};

const sessionListenerMiddleware = createListenerMiddleware();
sessionListenerMiddleware.startListening({
    actionCreator: logout,
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
            .concat(accountApi.middleware, spreadApi.middleware, subscriptionsAPI.middleware, mcpTokenApi.middleware),
});

export type AppDispatch = typeof store.dispatch;
export type IRootStore = ReturnType<typeof store.getState>;

export default store;