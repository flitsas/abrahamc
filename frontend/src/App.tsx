import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { AppLayout } from "./app/AppLayout.js";
import { ProtectedRoute } from "./features/auth/components/ProtectedRoute.js";
import { PermissionsStaleDialog } from "./shared/components/ui/PermissionsStaleDialog.js";
import { ActivatePage } from "./features/auth/pages/ActivatePage.js";
import { ForgotPasswordPage } from "./features/auth/pages/ForgotPasswordPage.js";
import { LoginPage } from "./features/auth/pages/LoginPage.js";
import { ResetPasswordPage } from "./features/auth/pages/ResetPasswordPage.js";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000 },
    mutations: { retry: 0 },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter
        future={{
          v7_startTransition: true,
          v7_relativeSplatPath: true,
        }}
      >
        <PermissionsStaleDialog />
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/activate" element={<ActivatePage />} />
          <Route path="/onboarding" element={<ActivatePage />} />
          <Route
            path="/*"
            element={
              <ProtectedRoute>
                <AppLayout />
              </ProtectedRoute>
            }
          />
        </Routes>
      </BrowserRouter>
      {import.meta.env.DEV && <ReactQueryDevtools />}
    </QueryClientProvider>
  );
}
