import React from 'react';
import { ThemeProvider as MuiThemeProvider, createTheme } from '@mui/material/styles';
import { useTheme } from './ThemeContext';

const MuiBridge: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const { theme } = useTheme();
    const muiTheme = createTheme({ palette: { mode: theme } });

    return (
        <MuiThemeProvider theme={muiTheme}>
            {children}
        </MuiThemeProvider>
    );
};

export default MuiBridge;
