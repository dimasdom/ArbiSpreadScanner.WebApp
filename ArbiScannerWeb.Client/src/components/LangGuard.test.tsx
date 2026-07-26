import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { LangGuard, RootRedirect } from './LangGuard';
import { createLocalStorageMock } from '../test/localStorageMock';

describe('LangGuard', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('renders the nested route when the :lang segment is supported', () => {
        render(
            <MemoryRouter initialEntries={['/en/faq']}>
                <Routes>
                    <Route path="/:lang" element={<LangGuard />}>
                        <Route path="faq" element={<div>FAQ Content</div>} />
                    </Route>
                </Routes>
            </MemoryRouter>,
        );

        expect(screen.getByText('FAQ Content')).toBeInTheDocument();
    });

    it('redirects to a detected-language URL when :lang is unsupported', () => {
        render(
            <MemoryRouter initialEntries={['/xx/faq']}>
                <Routes>
                    <Route path="/:lang" element={<LangGuard />}>
                        <Route path="faq" element={<div>FAQ Content</div>} />
                    </Route>
                    <Route path="/en/xx/faq" element={<div>Redirected FAQ</div>} />
                </Routes>
            </MemoryRouter>,
        );

        expect(screen.getByText('Redirected FAQ')).toBeInTheDocument();
    });

    it('redirects to a detected-language root when no :lang segment is present', () => {
        render(
            <MemoryRouter initialEntries={['/']}>
                <Routes>
                    <Route path="/" element={<LangGuard />} />
                    <Route path="/en/" element={<div>Redirected Root</div>} />
                </Routes>
            </MemoryRouter>,
        );

        expect(screen.getByText('Redirected Root')).toBeInTheDocument();
    });
});

describe('RootRedirect', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('redirects to the detected-language home page', () => {
        render(
            <MemoryRouter initialEntries={['/']}>
                <Routes>
                    <Route path="/" element={<RootRedirect />} />
                    <Route path="/en/" element={<div>Home</div>} />
                </Routes>
            </MemoryRouter>,
        );

        expect(screen.getByText('Home')).toBeInTheDocument();
    });
});
