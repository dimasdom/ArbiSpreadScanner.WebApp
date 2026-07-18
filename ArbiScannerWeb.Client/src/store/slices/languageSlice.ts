import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { getPreferredLanguage } from '../../i18n/detect';

export interface LanguageState {
    language: string;
}

const initialState: LanguageState = {
    language: getPreferredLanguage(),
};

const languageSlice = createSlice({
    name: 'language',
    initialState,
    reducers: {
        setLanguage(state, action: PayloadAction<string>) {
            state.language = action.payload;
        },
    },
});

export const { setLanguage } = languageSlice.actions;
export default languageSlice.reducer;
